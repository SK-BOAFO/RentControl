using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentControlSystem.Auth.API.DTOs;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Auth.API.Services;
using System.Security.Claims;

namespace RentControlSystem.Auth.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,RCD_Officer")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? role = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _userService.GetAllUsersAsync(role, isActive, page, pageSize);

                return Ok(new PaginatedApiResponse(
                    true,
                    "Users retrieved successfully",
                    result.Data,
                    result.TotalCount,
                    page,
                    pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                var result = await _userService.GetUserByIdAsync(id);

                if (!result.Success)
                    return NotFound(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "User retrieved", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("search/{searchTerm}")]
        public async Task<IActionResult> SearchUsers(string searchTerm)
        {
            try
            {
                var result = await _userService.SearchUsersAsync(searchTerm);

                return Ok(new ApiResponse(true, "Search results", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching users with term {SearchTerm}", searchTerm);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPut("{id}/profile")]
        [Authorize]
        public async Task<IActionResult> UpdateUserProfile(string id, [FromBody] UpdateProfileDto request)
        {
            try
            {
                // Check if user is updating their own profile or is admin/RCD officer
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

                if (currentUserId != id && !(currentUserRole == "Admin" || currentUserRole == "RCD_Officer"))
                    return Forbid();

                var result = await _userService.UpdateUserProfileAsync(id, request);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "Profile updated successfully", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPut("{id}/activate")]
        [Authorize(Roles = "Admin,RCD_Officer")]
        public async Task<IActionResult> ActivateUser(string id)
        {
            try
            {
                var result = await _userService.ActivateUserAsync(id);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "User activated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating user {UserId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPut("{id}/deactivate")]
        [Authorize(Roles = "Admin,RCD_Officer")]
        public async Task<IActionResult> DeactivateUser(string id)
        {
            try
            {
                var result = await _userService.DeactivateUserAsync(id);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "User deactivated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user {UserId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("{id}/roles")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUserRoles(string id, [FromBody] UpdateUserRolesDto request)
        {
            try
            {
                var result = await _userService.UpdateUserRolesAsync(id, request.Roles);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "User roles updated successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating roles for user {UserId}", id);
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _userService.GetUserByIdAsync(userId);

                if (!result.Success)
                    return NotFound(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "User retrieved", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }
    }
}