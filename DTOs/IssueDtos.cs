using System.ComponentModel.DataAnnotations;
using IssueFlow.Constants;

namespace IssueFlow.DTOs
{
    /// <summary>Read-only shape for API/views — no EF navigation cycles.</summary>
    public class IssueDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string CreatedById { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public List<string> AssignedDeveloperNames { get; set; } = new();
    }

    /// <summary>Create issue input — server sets Id/Status/CreatedBy.</summary>
    public class CreateIssueDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = Priorities.Medium;

        [Required(ErrorMessage = "Project is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid project.")]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        [StringLength(20)]
        [Display(Name = "Type")]
        public string? Type { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Deadline")]
        public DateTime? Deadline { get; set; }
    }

    /// <summary>Update issue input.</summary>
    public class UpdateIssueDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(30)]
        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = string.Empty;

        [Required(ErrorMessage = "Project is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid project.")]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Deadline")]
        public DateTime? Deadline { get; set; }
    }
}
