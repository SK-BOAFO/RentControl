using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Tenancy.API.DTOs;
using RentControlSystem.Tenancy.API.Services;
using System.Security.Claims;

namespace RentControlSystem.Tenancy.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TenancyController : ControllerBase
    {
        private readonly ITenancyService _tenancyService;
        private readonly ILogger<TenancyController> _logger;

        public TenancyController(
            ITenancyService tenancyService,
            ILogger<TenancyController> logger)
        {
            _tenancyService = tenancyService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenancy([FromBody] CreateTenancyDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.CreateTenancyAsync(dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, null, result.Errors));

                return CreatedAtAction(nameof(GetTenancyById),
                    new { id = result.Data?.Id },
                    new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenancy");
                return StatusCode(500, new ApiResponse(false, "An error occurred while creating tenancy"));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTenancyById(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.GetTenancyByIdAsync(id, userId);

                if (!result.Success)
                    return NotFound(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenancy {TenancyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchTenancies(
            [FromQuery] TenancySearchDto searchDto,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.SearchTenanciesAsync(searchDto, page, pageSize, userId);

                return Ok(new PaginatedApiResponse(
                    result.Success,
                    result.Message,
                    result.Data,
                    result.TotalCount,
                    page,
                    pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tenancies");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTenancy(Guid id, [FromBody] UpdateTenancyDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.UpdateTenancyAsync(id, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, null, result.Errors));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tenancy {TenancyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while updating tenancy"));
            }
        }

        [HttpPost("{id}/terminate")]
        public async Task<IActionResult> TerminateTenancy(Guid id, [FromBody] TerminateTenancyDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.TerminateTenancyAsync(id, dto.Reason, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating tenancy {TenancyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while terminating tenancy"));
            }
        }

        [HttpPost("{id}/renew")]
        public async Task<IActionResult> RenewTenancy(Guid id, [FromBody] RenewTenancyDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.RenewTenancyAsync(id, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, null, result.Errors));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing tenancy {TenancyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while renewing tenancy"));
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.GetTenancyStatisticsAsync(userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenancy statistics");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetTenantAgreements(string tenantId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.GetTenantAgreementsAsync(tenantId, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant agreements for tenant {TenantId}", tenantId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("landlord/{landlordId}")]
        public async Task<IActionResult> GetLandlordAgreements(string landlordId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.GetLandlordAgreementsAsync(landlordId, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting landlord agreements for landlord {LandlordId}", landlordId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("{id}/notice")]
        public async Task<IActionResult> IssueNotice(Guid id, [FromBody] IssueNoticeDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _tenancyService.IssueNoticeAsync(id, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error issuing notice for tenancy {TenancyId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while issuing notice"));
            }
        }
    }
}