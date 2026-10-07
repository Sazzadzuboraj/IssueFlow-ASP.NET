using IssueFlow.DTOs;
using IssueFlow.Models;
using IssueFlow.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IssueFlow.Controllers.Api
{
    /// <summary>
    /// REST API for Issues. Uses the same cookie Identity auth as the MVC app.
    /// For external clients, call /api/auth/login first (or use Swagger Authorize).
    /// </summary>
    [ApiController]
    [Route("api/issues")]
    [Authorize]
    [EnableRateLimiting("api")]
    [Produces("application/json")]
    public class IssuesApiController : ControllerBase
    {
        private readonly IIssueService _issueService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IssuesApiController(IIssueService issueService, UserManager<ApplicationUser> userManager)
        {
            _issueService = issueService;
            _userManager = userManager;
        }

        /// <summary>List issues with optional search/status/priority filters.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<IssueDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<IssueDto>>> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? priority)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isPrivileged = User.IsInRole("Admin") || User.IsInRole("ProjectManager") || User.IsInRole("QATester");
            var issues = await _issueService.GetIssuesAsync(search, status, priority, user, isPrivileged);
            var dtos = issues.Select(i => _issueService.ToDto(i)).ToList();
            return Ok(dtos);
        }

        /// <summary>Get a single issue by id.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(IssueDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IssueDto>> GetById(int id)
        {
            var issue = await _issueService.GetIssueDetailsAsync(id);
            if (issue == null) return NotFound(new { message = "Issue not found." });
            return Ok(_issueService.ToDto(issue));
        }

        /// <summary>Create a new issue.</summary>
        [HttpPost]
        [Authorize(Roles = "Admin,ProjectManager")]
        [ProducesResponseType(typeof(IssueDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IssueDto>> Create([FromBody] CreateIssueDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var issue = await _issueService.CreateIssueAsync(dto, user.Id);
            var created = await _issueService.GetIssueDetailsAsync(issue.Id);
            return CreatedAtAction(nameof(GetById), new { id = issue.Id }, _issueService.ToDto(created!));
        }

        /// <summary>Update an existing issue.</summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,ProjectManager")]
        [ProducesResponseType(typeof(IssueDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IssueDto>> Update(int id, [FromBody] UpdateIssueDto dto)
        {
            if (id != dto.Id) return BadRequest(new { message = "Id mismatch." });
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var ok = await _issueService.UpdateIssueAsync(dto);
            if (!ok) return NotFound(new { message = "Issue not found." });

            var updated = await _issueService.GetIssueDetailsAsync(id);
            return Ok(_issueService.ToDto(updated!));
        }
    }
}
