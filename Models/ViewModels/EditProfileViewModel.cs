using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace IssueFlow.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20)]
        [Display(Name = "Phone")]
        public string? PhoneNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Employee ID")]
        public string? EmployeeCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Position")]
        public string? Position { get; set; }

        [StringLength(300)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        public string? CurrentPhotoUrl { get; set; }

        [Display(Name = "New profile photo")]
        public IFormFile? Photo { get; set; }
    }
}
