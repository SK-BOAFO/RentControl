using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentControlSystem.API.DTOs;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.CaseManagement.API.DTOs;
using RentControlSystem.CaseManagement.API.Services;
using System.Security.Claims;

namespace RentControlSystem.CaseManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CasesController : ControllerBase
    {
        private readonly ICaseManagementService _caseService;
        private readonly IMediationService _mediationService;

        public CasesController(
            ICaseManagementService caseService,
            IMediationService mediationService)
        {
            _caseService = caseService;
            _mediationService = mediationService;
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResponse<CaseResponseDto>>> CreateCase(
            [FromBody] CreateCaseDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.CreateCaseAsync(dto, userId);
            return StatusCode(
                response.Success ? StatusCodes.Status201Created : StatusCodes.Status400BadRequest,
                response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceResponse<CaseResponseDto>>> GetCase(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.GetCaseByIdAsync(id, userId);
            return response.Success ? Ok(response) : NotFound(response);
        }

        [HttpPost("search")]
        public async Task<ActionResult<PaginatedServiceResponse<List<CaseResponseDto>>>> SearchCases(
            [FromBody] CaseSearchDto searchDto,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.SearchCasesAsync(searchDto, page, pageSize, userId);
            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceResponse<CaseResponseDto>>> UpdateCase(
            Guid id, [FromBody] UpdateCaseDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.UpdateCaseAsync(id, dto, userId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("{id}/submit")]
        public async Task<ActionResult<ServiceResponse<bool>>> SubmitCase(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.SubmitCaseAsync(id, userId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("{id}/assign")]
        [Authorize(Roles = "Admin,RCD_Officer")]
        public async Task<ActionResult<ServiceResponse<bool>>> AssignCase(
            Guid id, [FromBody] AssignCaseDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.AssignCaseAsync(id, dto, userId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("{id}/hearing")]
        [Authorize(Roles = "Admin,RCD_Officer")]
        public async Task<ActionResult<ServiceResponse<HearingResponseDto>>> ScheduleHearing(
            Guid id, [FromBody] ScheduleHearingDto dto)
        {
            dto.CaseId = id; // Ensure case ID matches route
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.ScheduleHearingAsync(dto, userId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<ServiceResponse<CaseStatisticsDto>>> GetStatistics()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.GetCaseStatisticsAsync(userId);
            return Ok(response);
        }

        [HttpGet("calendar")]
        public async Task<ActionResult<ServiceResponse<HearingCalendarDto>>> GetCalendar(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            fromDate ??= DateTime.UtcNow.Date;
            toDate ??= DateTime.UtcNow.AddMonths(1).Date;

            var response = await _caseService.GetHearingCalendarAsync(fromDate.Value, toDate.Value, userId);
            return Ok(response);
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<ServiceResponse<List<CaseDashboardDto>>>> GetDashboard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var response = await _caseService.GetDashboardCasesAsync(userId);
            return Ok(response);
        }
    }
}