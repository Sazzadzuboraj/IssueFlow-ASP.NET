using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    /// <summary>Jira-style project membership. Global Identity role ≠ project role.</summary>
    public class ProjectMember
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public Project? Project { get; set; }

        [Required(ErrorMessage = "User is required.")]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }

        [Required(ErrorMessage = "Project role is required.")]
        [StringLength(30)]
        [Display(Name = "Project role")]
        public string Role { get; set; } = "Developer";

        [Display(Name = "Joined")]
        [DataType(DataType.DateTime)]
        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    }
}
