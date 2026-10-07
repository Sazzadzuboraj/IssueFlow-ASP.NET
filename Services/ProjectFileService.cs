using IssueFlow.Models;
using IssueFlow.Repositories.Interfaces;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace IssueFlow.Services
{
    /// <summary>
    /// Physical layout:
    ///   wwwroot/uploads/projects/{Project.FolderPath}/
    ///       issues/{IssueId}/{FileType}/{guid}{ext}
    /// FolderPath is set on project create: "{Id}_{SanitizedName}".
    /// </summary>
    public class ProjectFileService : IProjectFileService
    {
        private readonly IUnitOfWork _uow;
        private readonly IWebHostEnvironment _env;
        private readonly IActivityLogService _activityLog;
        private readonly ILogger<ProjectFileService> _logger;

        // Source code + common project docs (not executables / archives that could be abusive).
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Source
            ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".kt", ".go", ".rs",
            ".c", ".cpp", ".h", ".hpp", ".swift", ".m", ".rb", ".php", ".vue", ".html",
            ".css", ".scss", ".sass", ".less", ".sql", ".sh", ".bash", ".ps1", ".bat",
            ".xml", ".json", ".yml", ".yaml", ".toml", ".ini", ".cfg", ".conf",
            ".md", ".txt", ".csv", ".tsv",
            // Documents / design notes
            ".pdf", ".doc", ".docx", ".rtf", ".odt",
            // Images (wireframes / screenshots of UI)
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg",
        };

        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB
        private const int MaxFilesPerUpload = 20;

        public ProjectFileService(
            IUnitOfWork uow,
            IWebHostEnvironment env,
            IActivityLogService activityLog,
            ILogger<ProjectFileService> logger)
        {
            _uow = uow;
            _env = env;
            _activityLog = activityLog;
            _logger = logger;
        }

        public string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "project";
            var s = name.Trim();
            s = Regex.Replace(s, @"[^\w\s\-]", "");
            s = Regex.Replace(s, @"\s+", "_");
            s = s.Trim('_');
            if (s.Length > 80) s = s[..80];
            return string.IsNullOrEmpty(s) ? "project" : s;
        }

        public async Task EnsureProjectFolderAsync(Project project)
        {
            if (project.Id <= 0)
                throw new InvalidOperationException("Project must be saved before creating its folder.");

            var folderName = $"{project.Id}_{SanitizeFolderName(project.Name)}";
            var physical = Path.Combine(_env.WebRootPath, "uploads", "projects", folderName);
            Directory.CreateDirectory(physical);
            // Subfolder seed so the tree is visible even before first upload
            Directory.CreateDirectory(Path.Combine(physical, "issues"));

            if (!string.Equals(project.FolderPath, folderName, StringComparison.Ordinal))
            {
                project.FolderPath = folderName;
                _uow.Projects.Update(project);
                await _uow.SaveChangesAsync();
            }

            _logger.LogInformation("Project folder ready: {Folder}", physical);
        }

        public async Task<(bool Ok, string? Error, List<IssueAttachment> Saved)> UploadIssueFilesAsync(
            int issueId,
            string uploaderId,
            string fileType,
            List<IFormFile> files)
        {
            var saved = new List<IssueAttachment>();

            if (files == null || files.Count == 0)
                return (false, "No files selected.", saved);

            if (files.Count > MaxFilesPerUpload)
                return (false, $"Maximum {MaxFilesPerUpload} files per upload.", saved);

            if (!IssueAttachmentTypes.IsValid(fileType))
                fileType = IssueAttachmentTypes.Source;

            var issue = await _uow.Issues.Query()
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Id == issueId);

            if (issue == null)
                return (false, "Issue not found.", saved);

            if (issue.Project == null)
                return (false, "Issue has no project.", saved);

            // Ensure project folder exists (covers projects created before this feature).
            if (string.IsNullOrWhiteSpace(issue.Project.FolderPath))
                await EnsureProjectFolderAsync(issue.Project);

            var projectFolder = issue.Project.FolderPath!;
            var typeFolder = fileType; // Source / Document / …

            var physicalDir = Path.Combine(
                _env.WebRootPath, "uploads", "projects", projectFolder, "issues",
                issueId.ToString(), typeFolder);
            Directory.CreateDirectory(physicalDir);

            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;
                if (file.Length > MaxFileSizeBytes)
                {
                    _logger.LogWarning("Skipped oversized file {Name} ({Size} bytes)", file.FileName, file.Length);
                    continue;
                }

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                {
                    _logger.LogWarning("Skipped disallowed extension: {Name}", file.FileName);
                    continue;
                }

                // Prevent path traversal in original name
                var originalName = Path.GetFileName(file.FileName);
                if (string.IsNullOrWhiteSpace(originalName)) continue;

                var safeStored = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
                var fullPath = Path.Combine(physicalDir, safeStored);

                await using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var webPath = $"/uploads/projects/{projectFolder}/issues/{issueId}/{typeFolder}/{safeStored}";

                var attachment = new IssueAttachment
                {
                    IssueId = issueId,
                    FileName = originalName,
                    FilePath = webPath,
                    ContentType = file.ContentType,
                    FileSizeBytes = file.Length,
                    FileType = fileType,
                    UploadedById = uploaderId,
                    UploadedDate = DateTime.UtcNow
                };

                await _uow.IssueAttachments.AddAsync(attachment);
                saved.Add(attachment);
            }

            if (saved.Count == 0)
                return (false, "No valid files were uploaded. Check size (max 20 MB) and allowed extensions.", saved);

            await _uow.SaveChangesAsync();

            await _activityLog.LogAsync(
                uploaderId,
                "SourceFilesUploaded",
                $"Uploaded {saved.Count} file(s) [{fileType}] to issue #{issueId}.",
                issueId: issueId);

            return (true, null, saved);
        }

        public async Task<List<IssueAttachment>> GetIssueAttachmentsAsync(int issueId)
        {
            return await _uow.IssueAttachments.Query()
                .Include(a => a.UploadedBy)
                .Where(a => a.IssueId == issueId)
                .OrderBy(a => a.FileType)
                .ThenByDescending(a => a.UploadedDate)
                .ToListAsync();
        }

        public async Task<IssueAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _uow.IssueAttachments.Query()
                .Include(a => a.UploadedBy)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
        }

        public async Task<(bool Ok, string? Error)> DeleteAttachmentAsync(
            int attachmentId, string requesterId, bool isPrivileged)
        {
            var att = await _uow.IssueAttachments.GetByIdAsync(attachmentId);
            if (att == null) return (false, "File not found.");

            if (!isPrivileged && att.UploadedById != requesterId)
                return (false, "You can only delete files you uploaded.");

            var physical = GetPhysicalPath(att.FilePath);
            if (physical != null && File.Exists(physical))
            {
                try { File.Delete(physical); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete physical file {Path}", physical);
                }
            }

            _uow.IssueAttachments.Remove(att);
            await _uow.SaveChangesAsync();

            await _activityLog.LogAsync(
                requesterId,
                "SourceFileDeleted",
                $"Deleted file '{att.FileName}' from issue #{att.IssueId}.",
                issueId: att.IssueId);

            return (true, null);
        }

        public async Task<(bool Ok, string? Error, Stream? ZipStream, string? DownloadFileName)> BuildIssueZipAsync(
            int issueId,
            string? fileType = null)
        {
            var issue = await _uow.Issues.Query()
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Id == issueId);

            if (issue == null)
                return (false, "Issue not found.", null, null);

            var query = _uow.IssueAttachments.Query()
                .Where(a => a.IssueId == issueId);

            if (!string.IsNullOrWhiteSpace(fileType) && IssueAttachmentTypes.IsValid(fileType))
                query = query.Where(a => a.FileType == fileType);

            var attachments = await query
                .OrderBy(a => a.FileType)
                .ThenBy(a => a.FileName)
                .ToListAsync();

            if (attachments.Count == 0)
                return (false, "No files to download.", null, null);

            var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Avoid duplicate entry names inside the zip
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var att in attachments)
                {
                    var physical = GetPhysicalPath(att.FilePath);
                    if (physical == null || !File.Exists(physical))
                    {
                        _logger.LogWarning("Skipping missing file for zip: {Path}", att.FilePath);
                        continue;
                    }

                    var safeName = Path.GetFileName(att.FileName);
                    if (string.IsNullOrWhiteSpace(safeName))
                        safeName = Path.GetFileName(physical);

                    var typeFolder = IssueAttachmentTypes.IsValid(att.FileType)
                        ? att.FileType
                        : IssueAttachmentTypes.Other;

                    var entryName = $"{typeFolder}/{safeName}";
                    entryName = DeduplicateEntryName(entryName, usedNames);
                    usedNames.Add(entryName);

                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var fileStream = new FileStream(physical, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await fileStream.CopyToAsync(entryStream);
                }
            }

            if (memory.Length == 0)
            {
                await memory.DisposeAsync();
                return (false, "No readable files found on disk.", null, null);
            }

            memory.Position = 0;

            var projectSlug = SanitizeFolderName(issue.Project?.Name ?? "project");
            var issueTitleSlug = SanitizeFolderName(
                string.IsNullOrWhiteSpace(issue.Title) ? $"issue-{issueId}" : issue.Title);
            if (issueTitleSlug.Length > 40) issueTitleSlug = issueTitleSlug[..40];

            var typeSuffix = !string.IsNullOrWhiteSpace(fileType) && IssueAttachmentTypes.IsValid(fileType)
                ? $"_{fileType}"
                : "";
            var downloadName = $"Issue{issueId}_{projectSlug}_{issueTitleSlug}{typeSuffix}.zip";

            return (true, null, memory, downloadName);
        }

        private static string DeduplicateEntryName(string entryName, HashSet<string> used)
        {
            if (!used.Contains(entryName)) return entryName;

            var dir = Path.GetDirectoryName(entryName)?.Replace('\\', '/') ?? "";
            var name = Path.GetFileNameWithoutExtension(entryName);
            var ext = Path.GetExtension(entryName);
            var i = 2;
            string candidate;
            do
            {
                candidate = string.IsNullOrEmpty(dir)
                    ? $"{name}_{i}{ext}"
                    : $"{dir}/{name}_{i}{ext}";
                i++;
            } while (used.Contains(candidate));

            return candidate;
        }

        public string? GetPhysicalPath(string webRelativePath)
        {
            if (string.IsNullOrWhiteSpace(webRelativePath)) return null;
            var relative = webRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            // Only allow paths under uploads/projects
            if (!relative.StartsWith($"uploads{Path.DirectorySeparatorChar}projects", StringComparison.OrdinalIgnoreCase))
                return null;
            var full = Path.GetFullPath(Path.Combine(_env.WebRootPath, relative));
            var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "projects"));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;
            return full;
        }
    }
}
