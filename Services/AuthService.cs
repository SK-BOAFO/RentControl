using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using RentControlSystem.API.Data;
using RentControlSystem.Auth.API.Data;
using RentControlSystem.Auth.API.DTOs;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Auth.API.Models;

namespace RentControlSystem.Auth.API.Services
{
    public interface IAuthService
    {
        Task<ServiceResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request);
        Task<ServiceResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request);
        Task<ServiceResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<ServiceResponse<bool>> LogoutAsync(string userId);
        Task<ServiceResponse<bool>> ChangePasswordAsync(string userId, ChangePasswordDto request);
        Task<ServiceResponse<bool>> RequestPasswordResetAsync(string email);
        Task<ServiceResponse<bool>> ResetPasswordAsync(ResetPasswordDto request);
    }

    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<AuthService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            IEmailService emailService,
            IDistributedCache cache,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _context = context;
            _emailService = emailService;
            _cache = cache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ServiceResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request)
        {
            try
            {
                // Validate request
                if (request == null)
                    return ServiceResponse<AuthResponseDto>.CreateError("Request cannot be null");

                if (string.IsNullOrEmpty(request.GhanaCardNumber))
                    return ServiceResponse<AuthResponseDto>.CreateError("Ghana Card number is required");

                if (string.IsNullOrEmpty(request.Email))
                    return ServiceResponse<AuthResponseDto>.CreateError("Email is required");

                if (request.Password != request.ConfirmPassword)
                    return ServiceResponse<AuthResponseDto>.CreateError("Passwords do not match");

                // Check if user already exists
                var existingUser = await _userManager.FindByNameAsync(request.GhanaCardNumber);
                if (existingUser != null)
                    return ServiceResponse<AuthResponseDto>.CreateError("User with this Ghana Card already exists");

                // Check if email is already registered
                var existingEmail = await _userManager.FindByEmailAsync(request.Email);
                if (existingEmail != null)
                    return ServiceResponse<AuthResponseDto>.CreateError("Email is already registered");

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = request.GhanaCardNumber,
                    Email = request.Email,
                    GhanaCardNumber = request.GhanaCardNumber,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    DateOfBirth = request.DateOfBirth,
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<AuthResponseDto>.CreateError($"Registration failed", errors);
                }

                // Use default role if none specified
                var role = !string.IsNullOrEmpty(request.Role) ? request.Role : "User";

                // Ensure role exists
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));

                // Add user to role
                await _userManager.AddToRoleAsync(user, role);

                // Update user's role property
                user.Role = role;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Create user profile
                var profile = new UserProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.UserProfiles.Add(profile);
                await _context.SaveChangesAsync();

                // Generate JWT token
                var authResponse = await GenerateJwtToken(user);

                // Log registration
                _logger.LogInformation("User registered: {GhanaCard}, Role: {Role}",
                    request.GhanaCardNumber, role);

                return ServiceResponse<AuthResponseDto>.CreateSuccess("Registration successful", authResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterAsync");
                return ServiceResponse<AuthResponseDto>.CreateError("An error occurred during registration");
            }
        }

        public async Task<ServiceResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.GhanaCardNumber))
                    return ServiceResponse<AuthResponseDto>.CreateError("Ghana Card number is required");

                if (string.IsNullOrEmpty(request.Password))
                    return ServiceResponse<AuthResponseDto>.CreateError("Password is required");

                var user = await _userManager.FindByNameAsync(request.GhanaCardNumber);
                if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                    return ServiceResponse<AuthResponseDto>.CreateError("Invalid Ghana Card or password");

                if (!user.IsActive)
                    return ServiceResponse<AuthResponseDto>.CreateError("Account is deactivated. Please contact administrator.");

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Generate JWT token
                var authResponse = await GenerateJwtToken(user);

                _logger.LogInformation("User logged in: {GhanaCard}", request.GhanaCardNumber);

                return ServiceResponse<AuthResponseDto>.CreateSuccess("Login successful", authResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoginAsync");
                return ServiceResponse<AuthResponseDto>.CreateError("An error occurred during login");
            }
        }

        private async Task<AuthResponseDto> GenerateJwtToken(ApplicationUser user)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("GhanaCard", user.GhanaCardNumber),
                new Claim("FullName", $"{user.FirstName} {user.LastName}"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
                authClaims.Add(new Claim("role", userRole));
            }

            var jwtKey = _configuration["JWT:Secret"] ??
                        _configuration["JWT:Key"] ??
                        throw new ArgumentNullException("JWT Secret key is not configured");

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"] ?? "RentControlSystem",
                audience: _configuration["JWT:ValidAudience"] ?? "RentControlSystemUsers",
                expires: DateTime.Now.AddHours(3),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Generate refresh token
            var refreshToken = new RefreshToken
            {
                JwtId = token.Id,
                UserId = user.Id,
                Token = GenerateRefreshToken(),
                ExpiryDate = DateTime.UtcNow.AddDays(7),
                IsUsed = false,
                IsRevoked = false,
                CreatedDate = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                Id = user.Id,
                GhanaCardNumber = user.GhanaCardNumber,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role ?? "User",
                Token = tokenString,
                RefreshToken = refreshToken.Token,
                TokenExpiry = token.ValidTo
            };
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public async Task<ServiceResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.RefreshToken))
                    return ServiceResponse<AuthResponseDto>.CreateError("Token and refresh token are required");

                var principal = GetPrincipalFromExpiredToken(request.Token);
                if (principal == null)
                    return ServiceResponse<AuthResponseDto>.CreateError("Invalid token");

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return ServiceResponse<AuthResponseDto>.CreateError("Invalid token");

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                    return ServiceResponse<AuthResponseDto>.CreateError("User not found or inactive");

                var storedRefreshToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && rt.UserId == userId);

                if (storedRefreshToken == null || storedRefreshToken.IsUsed || storedRefreshToken.IsRevoked)
                    return ServiceResponse<AuthResponseDto>.CreateError("Invalid refresh token");

                if (storedRefreshToken.ExpiryDate < DateTime.UtcNow)
                    return ServiceResponse<AuthResponseDto>.CreateError("Refresh token expired");

                // Mark refresh token as used
                storedRefreshToken.IsUsed = true;
                storedRefreshToken.ModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Generate new token
                var authResponse = await GenerateJwtToken(user);

                return ServiceResponse<AuthResponseDto>.CreateSuccess("Token refreshed", authResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RefreshTokenAsync");
                return ServiceResponse<AuthResponseDto>.CreateError("An error occurred refreshing token");
            }
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var jwtKey = _configuration["JWT:Secret"] ??
                            _configuration["JWT:Key"] ??
                            throw new ArgumentNullException("JWT Secret key is not configured");

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateLifetime = false
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                return principal;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ServiceResponse<bool>> LogoutAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return ServiceResponse<bool>.CreateError("User ID is required");

                // Revoke all refresh tokens for user
                var refreshTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                    .ToListAsync();

                foreach (var token in refreshTokens)
                {
                    token.IsRevoked = true;
                    token.ModifiedDate = DateTime.UtcNow;
                }

                if (refreshTokens.Any())
                    await _context.SaveChangesAsync();

                // Clear user cache
                var cacheKey = $"user_{userId}";
                await _cache.RemoveAsync(cacheKey);

                return ServiceResponse<bool>.CreateSuccess("Logout successful", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LogoutAsync");
                return ServiceResponse<bool>.CreateError("An error occurred during logout");
            }
        }

        public async Task<ServiceResponse<bool>> ChangePasswordAsync(string userId, ChangePasswordDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return ServiceResponse<bool>.CreateError("User ID is required");

                if (request.NewPassword != request.ConfirmPassword)
                    return ServiceResponse<bool>.CreateError("New password and confirmation do not match");

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return ServiceResponse<bool>.CreateError("User not found");

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<bool>.CreateError("Password change failed", errors);
                }

                // Update UpdatedAt timestamp
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Log password change
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Action = "CHANGE_PASSWORD",
                    EntityType = "User",
                    EntityId = userId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = GetIpAddress()
                });

                await _context.SaveChangesAsync();

                return ServiceResponse<bool>.CreateSuccess("Password changed successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ChangePasswordAsync");
                return ServiceResponse<bool>.CreateError("An error occurred changing password");
            }
        }

        public async Task<ServiceResponse<bool>> RequestPasswordResetAsync(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return ServiceResponse<bool>.CreateError("Email is required");

                var user = await _userManager.FindByEmailAsync(email);

                // For security, don't reveal if user exists or not
                if (user == null)
                {
                    // Still return success to prevent email enumeration attacks
                    _logger.LogInformation("Password reset requested for non-existent email: {Email}", email);
                    return ServiceResponse<bool>.CreateSuccess("If your email is registered, you will receive a password reset link", true);
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // Store token in cache with 15-minute expiry
                var cacheKey = $"password_reset_{user.Id}";
                await _cache.SetStringAsync(cacheKey, token, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                });

                // Send email with reset link
                var resetLink = $"{_configuration["ClientApp:Url"]}/reset-password?token={WebUtility.UrlEncode(token)}&userId={user.Id}";

                var emailBody = $@"
                    <h3>Password Reset Request</h3>
                    <p>You have requested to reset your password for the Ghana Rent Control System.</p>
                    <p>Click the link below to reset your password (valid for 15 minutes):</p>
                    <p><a href='{resetLink}'>{resetLink}</a></p>
                    <p>If you did not request this, please ignore this email.</p>
                ";

                await _emailService.SendEmailAsync(user.Email, "Password Reset Request", emailBody);

                _logger.LogInformation("Password reset email sent to: {Email}", email);

                return ServiceResponse<bool>.CreateSuccess("If your email is registered, you will receive a password reset link", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RequestPasswordResetAsync");
                return ServiceResponse<bool>.CreateError("An error occurred processing your request");
            }
        }

        public async Task<ServiceResponse<bool>> ResetPasswordAsync(ResetPasswordDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                    return ServiceResponse<bool>.CreateError("User ID is required");

                if (string.IsNullOrEmpty(request.Token))
                    return ServiceResponse<bool>.CreateError("Reset token is required");

                if (request.NewPassword != request.ConfirmPassword)
                    return ServiceResponse<bool>.CreateError("Passwords do not match");

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                    return ServiceResponse<bool>.CreateError("User not found");

                // Verify token from cache
                var cacheKey = $"password_reset_{user.Id}";
                var cachedToken = await _cache.GetStringAsync(cacheKey);

                if (cachedToken != request.Token)
                    return ServiceResponse<bool>.CreateError("Invalid or expired reset token");

                var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<bool>.CreateError("Password reset failed", errors);
                }

                // Remove used token from cache
                await _cache.RemoveAsync(cacheKey);

                // Update UpdatedAt timestamp
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Log password reset
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = user.Id,
                    Action = "RESET_PASSWORD",
                    EntityType = "User",
                    EntityId = user.Id,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = GetIpAddress()
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset successful for user: {UserId}", user.Id);

                return ServiceResponse<bool>.CreateSuccess("Password reset successful", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPasswordAsync");
                return ServiceResponse<bool>.CreateError("An error occurred resetting password");
            }
        }

        private string GetIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }
}