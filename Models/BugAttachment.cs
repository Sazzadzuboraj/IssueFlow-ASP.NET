using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class BugAttachment
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int BugId { get; set; }

        [ForeignKey(nameof(BugId))]
        public Bug? Bug { get; set; }

        [Required(ErrorMessage = "File name is required.")]
        [StringLength(260)]
        [Display(Name = "File name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Content type")]
        public string? ContentType { get; set; }

        [Range(0, 10_485_760, ErrorMessage = "File size cannot exceed 10 MB.")]
        [Display(Name = "Size (bytes)")]
        public long FileSizeBytes { get; set; }

        [Required]
        [StringLength(450)]
        public string UploadedById { get; set; } = string.Empty;

        [ForeignKey(nameof(UploadedById))]
        public ApplicationUser? UploadedBy { get; set; }

        [Display(Name = "Uploaded")]
        [DataType(DataType.DateTime)]
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    }
}
