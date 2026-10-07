using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using IssueFlow.Constants;

namespace IssueFlow.Models.ViewModels
{
    public class AssignBugViewModel
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int BugId { get; set; }

        [Display(Name = "Bug")]
        public string BugTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a developer.")]
        [StringLength(450)]
        [Display(Name = "Developer")]
        public string DeveloperId { get; set; } = string.Empty;

        public List<SelectListItem> Developers { get; set; } = new();
    }

    public class ReviewFixViewModel
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int BugId { get; set; }

        [Display(Name = "Bug")]
        public string BugTitle { get; set; } = string.Empty;

        public string? CurrentStatus { get; set; }

        [Required]
        [Display(Name = "Approve fix")]
        public bool Approve { get; set; }

        [StringLength(1000, ErrorMessage = "Review notes cannot exceed 1000 characters.")]
        [Display(Name = "Review notes")]
        [DataType(DataType.MultilineText)]
        public string? ReviewNotes { get; set; }
    }

    public class ReportBugViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        [Display(Name = "Priority")]
        public string Priority { get; set; } = global::IssueFlow.Constants.Priorities.Medium;

        [Required(ErrorMessage = "Project is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid project.")]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        public List<SelectListItem> Projects { get; set; } = new();

        /// <summary>Dropdown options (named PriorityOptions to avoid clash with global::IssueFlow.Constants.Priorities).</summary>
        public List<SelectListItem> PriorityOptions { get; set; } = new()
        {
            new(global::IssueFlow.Constants.Priorities.Low, global::IssueFlow.Constants.Priorities.Low),
            new(global::IssueFlow.Constants.Priorities.Medium, global::IssueFlow.Constants.Priorities.Medium),
            new(global::IssueFlow.Constants.Priorities.High, global::IssueFlow.Constants.Priorities.High),
            new(global::IssueFlow.Constants.Priorities.Critical, global::IssueFlow.Constants.Priorities.Critical)
        };

        [Display(Name = "Screenshots")]
        public List<IFormFile>? Attachments { get; set; }
    }

    public class RequestExtensionViewModel
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int IssueId { get; set; }

        [Display(Name = "Issue")]
        public string IssueTitle { get; set; } = string.Empty;

        [Display(Name = "Current deadline")]
        [DataType(DataType.Date)]
        public DateTime? CurrentDeadline { get; set; }

        [Required(ErrorMessage = "New deadline is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Requested new deadline")]
        public DateTime RequestedNewDeadline { get; set; }

        [Required(ErrorMessage = "Reason is required.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
        [Display(Name = "Reason")]
        [DataType(DataType.MultilineText)]
        public string Reason { get; set; } = string.Empty;
    }
}
