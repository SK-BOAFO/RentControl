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
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message, result.Errors));

                return Ok(new ApiResponse(true, "Registration successful", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {GhanaCard}", request.GhanaCardNumber);
                return StatusCode(500, new ApiResponse(false, "An error occurred during registration"));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);

                if (!result.Success)
                    return Unauthorized(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "Login successful", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {GhanaCard}", request.GhanaCardNumber);
                return StatusCode(500, new ApiResponse(false, "An error occurred during login"));
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request);

                if (!result.Success)
                    return Unauthorized(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "Token refreshed", result.Data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new ApiResponse(false, "An error occurred refreshing token"));
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                await _authService.LogoutAsync(userId);

                return Ok(new ApiResponse(true, "Logout successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new ApiResponse(false, "An error occurred during logout"));
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ApiResponse(false, "Invalid user"));

                var result = await _authService.ChangePasswordAsync(userId, request);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "Password changed successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, new ApiResponse(false, "An error occurred changing password"));
            }
        }

        [HttpPost("reset-password-request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ForgotPasswordRequestDto request)
        {
            try
            {
                var result = await _authService.RequestPasswordResetAsync(request.Email);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "Password reset link sent to email"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(request);

                if (!result.Success)
                    return BadRequest(new ApiResponse(false, result.Message));

                return Ok(new ApiResponse(true, "Password reset successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(500, new ApiResponse(false, "An error occurred"));
            }
        }
    }
}