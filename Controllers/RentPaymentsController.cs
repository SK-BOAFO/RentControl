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
    public class RentPaymentsController : ControllerBase
    {
        private readonly IRentPaymentService _rentPaymentService;
        private readonly ILogger<RentPaymentsController> _logger;

        public RentPaymentsController(
            IRentPaymentService rentPaymentService,
            ILogger<RentPaymentsController> logger)
        {
            _rentPaymentService = rentPaymentService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] CreateRentPaymentDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.CreateRentPaymentAsync(dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, null, result.Errors));

                return CreatedAtAction(nameof(GetPaymentById),
                    new { id = result.Data?.Id },
                    new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rent payment");
                return StatusCode(500, new ApiResponse(false, "An error occurred while creating payment"));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPaymentById(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.GetPaymentByIdAsync(id, userId);

                if (!result.Success)
                    return NotFound(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("tenancy/{tenancyId}")]
        public async Task<IActionResult> GetPaymentsByTenancy(
            Guid tenancyId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.GetPaymentsByTenancyAsync(
                    tenancyId, fromDate, toDate, page, pageSize, userId);

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
                _logger.LogError(ex, "Error getting payments for tenancy {TenancyId}", tenancyId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetPaymentsByTenant(
            string tenantId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.GetPaymentsByTenantAsync(
                    tenantId, fromDate, toDate, page, pageSize, userId);

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
                _logger.LogError(ex, "Error getting payments for tenant {TenantId}", tenantId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("landlord/{landlordId}")]
        public async Task<IActionResult> GetPaymentsByLandlord(
            string landlordId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.GetPaymentsByLandlordAsync(
                    landlordId, fromDate, toDate, page, pageSize, userId);

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
                _logger.LogError(ex, "Error getting payments for landlord {LandlordId}", landlordId);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("{id}/verify")]
        [Authorize(Roles = "Admin,RCD_Officer,Landlord")]
        public async Task<IActionResult> VerifyPayment(Guid id, [FromBody] VerifyPaymentDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.VerifyPaymentAsync(id, dto, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment {PaymentId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred while verifying payment"));
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetPaymentStatistics(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.GetPaymentStatisticsAsync(
                    fromDate, toDate, userId);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message, result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment statistics");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("overdue")]
        [Authorize(Roles = "Admin,RCD_Officer,Landlord")]
        public async Task<IActionResult> GetOverduePayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _rentPaymentService.GetOverduePaymentsAsync(page, pageSize, userId);

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
                _logger.LogError(ex, "Error getting overdue payments");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("mobile-money/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> MobileMoneyCallback([FromBody] MobileMoneyCallbackDto dto)
        {
            try
            {
                var result = await _rentPaymentService.ProcessMobileMoneyCallbackAsync(dto);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, result.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mobile money callback");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }
    }
}