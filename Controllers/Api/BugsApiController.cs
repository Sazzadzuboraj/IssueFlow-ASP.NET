using IssueFlow.DTOs;
using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IssueFlow.Controllers.Api
{
    [ApiController]
    [Route("api/bugs")]
    [Authorize]
    [EnableRateLimiting("api")]
    [Produces("application/json")]
    public class BugsApiController : ControllerBase
    {
        private readonly IBugService _bugService;
        private readonly UserManager<ApplicationUser> _userManager;

        public BugsApiController(IBugService bugService, UserManager<ApplicationUser> userManager)
        {
            _bugService = bugService;
            _userManager = userManager;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<BugDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<BugDto>>> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? priority)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager");
            var bugs = await _bugService.GetBugsAsync(search, status, priority, user, isPrivileged);
            return Ok(bugs.Select(ToDto).ToList());
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(BugDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BugDto>> GetById(int id)
        {
            var bug = await _bugService.GetBugDetailsAsync(id);
            if (bug == null) return NotFound(new { message = "Bug not found." });
            return Ok(ToDto(bug));
        }

        [HttpPost]
        [Authorize(Roles = "QATester")]
        [ProducesResponseType(typeof(BugDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<BugDto>> Report([FromBody] CreateBugDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var bug = await _bugService.ReportBugAsync(dto, user.Id, null);
            var created = await _bugService.GetBugDetailsAsync(bug.Id);
            return CreatedAtAction(nameof(GetById), new { id = bug.Id }, ToDto(created!));
        }

        [HttpPost("{id:int}/assign")]
        [Authorize(Roles = "Admin,ProjectManager")]
        public async Task<IActionResult> Assign(int id, [FromBody] AssignBugDto dto)
        {
            dto.BugId = id;
            var ok = await _bugService.AssignBugAsync(dto);
            if (!ok) return NotFound(new { message = "Bug not found." });
            return Ok(new { message = "Bug assigned." });
        }

        [HttpPost("{id:int}/submit-fix")]
        [Authorize(Roles = "Developer,Admin,ProjectManager")]
        public async Task<IActionResult> SubmitFix(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            try
            {
                var ok = await _bugService.SubmitFixAsync(id, user.Id);
                if (!ok) return NotFound(new { message = "Bug not found." });
                return Ok(new { message = "Fix submitted for review." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpPost("{id:int}/review")]
        [Authorize(Roles = "QATester,Admin,ProjectManager")]
        public async Task<IActionResult> Review(int id, [FromBody] ReviewBugDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var ok = await _bugService.ReviewFixAsync(id, user.Id, dto.Approve, dto.ReviewNotes);
            if (!ok) return NotFound(new { message = "Bug not found." });
            return Ok(new { message = dto.Approve ? "Bug verified." : "Bug reopened." });
        }

        private static BugDto ToDto(Bug b) => new()
        {
            Id = b.Id,
            Title = b.Title,
            Description = b.Description,
            Status = b.Status,
            Priority = b.Priority,
            ProjectId = b.ProjectId,
            ProjectName = b.Project?.Name,
            ReportedById = b.ReportedById,
            ReportedByName = b.ReportedBy?.FullName ?? b.ReportedBy?.Email,
            AssignedDeveloperId = b.AssignedDeveloperId,
            AssignedDeveloperName = b.AssignedDeveloper?.FullName ?? b.AssignedDeveloper?.Email,
            CreatedDate = b.CreatedDate,
            UpdatedDate = b.UpdatedDate,
            VerifiedDate = b.VerifiedDate,
            ReviewNotes = b.ReviewNotes
        };
    }

    public class ReviewBugDto
    {
        public bool Approve { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
