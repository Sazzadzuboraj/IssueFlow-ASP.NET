using IssueFlow.Models;
using Microsoft.AspNetCore.Http;

namespace IssueFlow.Services.Interfaces
{
    public interface IUserProfileService
    {
        /// <summary>Saves a profile photo to wwwroot/uploads/profiles and returns its relative URL.</summary>
        Task<string?> SavePhotoAsync(string userId, IFormFile? photo);

        Task UpdateProfileAsync(ApplicationUser user, string? position, string? address, string? employeeCode, string? phoneNumber, string? photoUrl);
    }
}
