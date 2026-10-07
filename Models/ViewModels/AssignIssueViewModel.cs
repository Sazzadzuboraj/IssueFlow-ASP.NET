using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IssueFlow.Models.ViewModels
{
    public class AssignIssueViewModel
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int IssueId { get; set; }

        [Display(Name = "Issue")]
        public string IssueTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a developer.")]
        [StringLength(450)]
        [Display(Name = "Developer")]
        public string UserId { get; set; } = string.Empty;

        public List<SelectListItem>? Developers { get; set; }
    }
}
