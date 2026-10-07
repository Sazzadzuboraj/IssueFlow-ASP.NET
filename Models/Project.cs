using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IssueFlow.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Project name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters.")]
        [Display(Name = "Project name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        [Display(Name = "Description")]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Display(Name = "Created")]
        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(450)]
        public string CreatedById { get; set; } = string.Empty;

        [ForeignKey(nameof(CreatedById))]
        public ApplicationUser? CreatedBy { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Physical folder name under wwwroot/uploads/projects/
        /// Format: {Id}_{SanitizedName} — created when the project is saved.
        /// </summary>
        [StringLength(200)]
        [Display(Name = "Folder path")]
        public string? FolderPath { get; set; }

        public ICollection<Issue>? Issues { get; set; }
        public ICollection<ProjectMember>? Members { get; set; }
    }
}
