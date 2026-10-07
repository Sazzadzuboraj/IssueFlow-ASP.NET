using IssueFlow.Data;
using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace IssueFlow.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
        private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB — profile photos should be small

        public UserProfileService(IWebHostEnvironment env, ApplicationDbContext context)
        {
            _env = env;
            _context = context;
        }

        public async Task<string?> SavePhotoAsync(string userId, IFormFile? photo)
        {
            if (photo == null || photo.Length == 0) return null;
            if (photo.Length > MaxFileSizeBytes) return null;

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(folder);

            var safeName = $"{userId}_{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(folder, safeName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            return $"/uploads/profiles/{safeName}";
        }

        public async Task UpdateProfileAsync(ApplicationUser user, string? position, string? address, string? employeeCode, string? phoneNumber, string? photoUrl)
        {
            user.Position = position;
            user.Address = address;
            user.EmployeeCode = employeeCode;
            user.PhoneNumber = phoneNumber;
            if (!string.IsNullOrEmpty(photoUrl))
                user.PhotoUrl = photoUrl;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
    }
}
