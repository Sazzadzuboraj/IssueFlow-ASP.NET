using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    /// <summary>
    /// Source / document file submitted by a developer against an issue.
    /// Stored under the project's physical folder:
    /// wwwroot/uploads/projects/{ProjectFolder}/issues/{IssueId}/{FileType}/
    /// </summary>
    public class IssueAttachment
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int IssueId { get; set; }

        [ForeignKey(nameof(IssueId))]
        public Issue? Issue { get; set; }

        [Required(ErrorMessage = "File name is required.")]
        [StringLength(260)]
        [Display(Name = "File name")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>Web-relative path, e.g. /uploads/projects/12_MyApp/issues/5/Source/abc.cs</summary>
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Content type")]
        public string? ContentType { get; set; }

        [Range(0, 20_971_520, ErrorMessage = "File size cannot exceed 20 MB.")]
        [Display(Name = "Size (bytes)")]
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Logical folder / category inside the issue:
        /// Source, Document, Config, Test, Other
        /// </summary>
        [Required]
        [StringLength(40)]
        [Display(Name = "File type")]
        public string FileType { get; set; } = IssueAttachmentTypes.Source;

        [Required]
        [StringLength(450)]
        public string UploadedById { get; set; } = string.Empty;

        [ForeignKey(nameof(UploadedById))]
        public ApplicationUser? UploadedBy { get; set; }

        [Display(Name = "Uploaded")]
        [DataType(DataType.DateTime)]
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Allowed category folders for issue source files.</summary>
    public static class IssueAttachmentTypes
    {
        public const string Source = "Source";
        public const string Document = "Document";
        public const string Config = "Config";
        public const string Test = "Test";
        public const string Other = "Other";

        public static readonly string[] All =
        {
            Source, Document, Config, Test, Other
        };

        public static bool IsValid(string? type)
            => !string.IsNullOrWhiteSpace(type) && All.Contains(type);
    }
}
