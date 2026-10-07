using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace IssueFlow.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Employee ID")]
        public string? EmployeeCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Position")]
        public string? Position { get; set; }

        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20)]
        [Display(Name = "Phone")]
        public string? PhoneNumber { get; set; }

        [StringLength(300)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Profile photo")]
        public IFormFile? Photo { get; set; }
    }
}
