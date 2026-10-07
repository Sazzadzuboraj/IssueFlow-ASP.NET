using IssueFlow.Models;
using Microsoft.AspNetCore.Http;

namespace IssueFlow.Services.Interfaces
{
    /// <summary>
    /// Project-scoped file system: creates project folders on disk and
    /// manages issue source-file uploads organized by type (Source, Document, …).
    /// </summary>
    public interface IProjectFileService
    {
        /// <summary>Create wwwroot/uploads/projects/{folderPath} and set Project.FolderPath.</summary>
        Task EnsureProjectFolderAsync(Project project);

        /// <summary>Sanitize a project name for use as a folder segment.</summary>
        string SanitizeFolderName(string name);

        Task<(bool Ok, string? Error, List<IssueAttachment> Saved)> UploadIssueFilesAsync(
            int issueId,
            string uploaderId,
            string fileType,
            List<IFormFile> files);

        Task<List<IssueAttachment>> GetIssueAttachmentsAsync(int issueId);

        Task<IssueAttachment?> GetAttachmentByIdAsync(int attachmentId);

        Task<(bool Ok, string? Error)> DeleteAttachmentAsync(int attachmentId, string requesterId, bool isPrivileged);

        /// <summary>
        /// Build a ZIP of all (or one FileType) attachments for an issue.
        /// Entries keep folder layout: {FileType}/{FileName}.
        /// Returns null Ok with error if no files / issue missing.
        /// Caller must dispose the stream.
        /// </summary>
        Task<(bool Ok, string? Error, Stream? ZipStream, string? DownloadFileName)> BuildIssueZipAsync(
            int issueId,
            string? fileType = null);

        /// <summary>Map web-relative FilePath to absolute path under wwwroot.</summary>
        string? GetPhysicalPath(string webRelativePath);
    }
}
