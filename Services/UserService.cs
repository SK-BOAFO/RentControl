using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentControlSystem.API.Data;
using RentControlSystem.Auth.API.Data;
using RentControlSystem.Auth.API.DTOs;
using RentControlSystem.Auth.API.Helpers;
using RentControlSystem.Auth.API.Models;

namespace RentControlSystem.Auth.API.Services
{
    public interface IUserService
    {
        Task<PaginatedServiceResponse<List<UserResponseDto>>> GetAllUsersAsync(
            string? role, bool? isActive, int page, int pageSize);
        Task<ServiceResponse<UserResponseDto>> GetUserByIdAsync(string id);
        Task<ServiceResponse<List<UserResponseDto>>> SearchUsersAsync(string searchTerm);
        Task<ServiceResponse<UserResponseDto>> UpdateUserProfileAsync(string id, UpdateProfileDto request);
        Task<ServiceResponse<bool>> ActivateUserAsync(string id);
        Task<ServiceResponse<bool>> DeactivateUserAsync(string id);
        Task<ServiceResponse<bool>> UpdateUserRolesAsync(string id, List<string> roles);
    }

    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        public async Task<PaginatedServiceResponse<List<UserResponseDto>>> GetAllUsersAsync(
            string? role, bool? isActive, int page, int pageSize)
        {
            try
            {
                // Validate pagination
                page = page < 1 ? 1 : page;
                pageSize = pageSize < 1 ? 10 : pageSize > 100 ? 100 : pageSize;

                var query = _userManager.Users.AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(role))
                    query = query.Where(u => u.Role == role);

                if (isActive.HasValue)
                    query = query.Where(u => u.IsActive == isActive.Value);

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                var users = await query
                    .Include(u => u.Profile)
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userDtos = MapToUserResponseDtoList(users);

                return PaginatedServiceResponse<List<UserResponseDto>>.CreateSuccess(
                    "Users retrieved successfully",
                    userDtos,
                    totalCount,
                    page,
                    pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllUsersAsync");
                return PaginatedServiceResponse<List<UserResponseDto>>.CreateError("An error occurred retrieving users");
            }
        }

        public async Task<ServiceResponse<UserResponseDto>> GetUserByIdAsync(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return ServiceResponse<UserResponseDto>.CreateError("User ID is required");

                var user = await _userManager.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                    return ServiceResponse<UserResponseDto>.CreateError("User not found");

                var userDto = MapToUserResponseDto(user);
                return ServiceResponse<UserResponseDto>.CreateSuccess("User retrieved successfully", userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetUserByIdAsync for user {UserId}", id);
                return ServiceResponse<UserResponseDto>.CreateError("An error occurred retrieving user");
            }
        }

        public async Task<ServiceResponse<List<UserResponseDto>>> SearchUsersAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return ServiceResponse<List<UserResponseDto>>.CreateError("Search term is required");

                if (searchTerm.Length < 2)
                    return ServiceResponse<List<UserResponseDto>>.CreateSuccess(
                        "Search term too short, please use at least 2 characters",
                        new List<UserResponseDto>());

                var normalizedSearchTerm = searchTerm.ToUpper();

                var users = await _userManager.Users
                    .Include(u => u.Profile)
                    .Where(u => u.GhanaCardNumber.ToUpper().Contains(normalizedSearchTerm) ||
                               u.Email.ToUpper().Contains(normalizedSearchTerm) ||
                               u.PhoneNumber.Contains(searchTerm) ||
                               (u.FirstName.ToUpper() + " " + u.LastName.ToUpper()).Contains(normalizedSearchTerm) ||
                               u.UserName.ToUpper().Contains(normalizedSearchTerm))
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Take(50)
                    .ToListAsync();

                var userDtos = MapToUserResponseDtoList(users);
                return ServiceResponse<List<UserResponseDto>>.CreateSuccess("Search completed", userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchUsersAsync with term {SearchTerm}", searchTerm);
                return ServiceResponse<List<UserResponseDto>>.CreateError("An error occurred during search");
            }
        }

        public async Task<ServiceResponse<UserResponseDto>> UpdateUserProfileAsync(string id, UpdateProfileDto request)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return ServiceResponse<UserResponseDto>.CreateError("User ID is required");

                if (request == null)
                    return ServiceResponse<UserResponseDto>.CreateError("Request cannot be null");

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return ServiceResponse<UserResponseDto>.CreateError("User not found");

                // Update user properties if provided
                if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != user.PhoneNumber)
                {
                    // Check if phone number is already in use by another user
                    var existingPhone = await _userManager.Users
                        .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber && u.Id != id);

                    if (existingPhone != null)
                        return ServiceResponse<UserResponseDto>.CreateError("Phone number is already in use");

                    user.PhoneNumber = request.PhoneNumber;
                }

                if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
                {
                    // Check if email is already in use by another user
                    var existingEmail = await _userManager.FindByEmailAsync(request.Email);
                    if (existingEmail != null && existingEmail.Id != id)
                        return ServiceResponse<UserResponseDto>.CreateError("Email is already in use");

                    user.Email = request.Email;
                    user.NormalizedEmail = request.Email.ToUpper();
                }

                if (!string.IsNullOrEmpty(request.FirstName))
                    user.FirstName = request.FirstName;

                if (!string.IsNullOrEmpty(request.LastName))
                    user.LastName = request.LastName;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = updateResult.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<UserResponseDto>.CreateError("Update failed", errors);
                }

                // Update or create profile
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
                if (profile == null)
                {
                    profile = new UserProfile
                    {
                        Id = Guid.NewGuid().ToString(),
                        UserId = id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.UserProfiles.Add(profile);
                }

                if (!string.IsNullOrEmpty(request.Address))
                    profile.Address = request.Address;

                if (!string.IsNullOrEmpty(request.City))
                    profile.City = request.City;

                if (!string.IsNullOrEmpty(request.Region))
                    profile.Region = request.Region;

                if (!string.IsNullOrEmpty(request.PostalCode))
                    profile.PostalCode = request.PostalCode;

                if (!string.IsNullOrEmpty(request.EmergencyContact))
                    profile.EmergencyContact = request.EmergencyContact;

                if (!string.IsNullOrEmpty(request.EmergencyPhone))
                    profile.EmergencyPhone = request.EmergencyPhone;

                profile.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Get updated user with profile
                var updatedUser = await _userManager.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == id);

                var userDto = MapToUserResponseDto(updatedUser);
                return ServiceResponse<UserResponseDto>.CreateSuccess("Profile updated successfully", userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateUserProfileAsync for user {UserId}", id);
                return ServiceResponse<UserResponseDto>.CreateError("An error occurred updating profile");
            }
        }

        public async Task<ServiceResponse<bool>> ActivateUserAsync(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return ServiceResponse<bool>.CreateError("User ID is required");

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return ServiceResponse<bool>.CreateError("User not found");

                if (user.IsActive)
                    return ServiceResponse<bool>.CreateSuccess("User is already active", true);

                user.IsActive = true;
                user.ActivatedAt = DateTime.UtcNow;
                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<bool>.CreateError("Activation failed", errors);
                }

                // Log activation
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = id,
                    Action = "ACTIVATE_USER",
                    EntityType = "User",
                    EntityId = id,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Unknown" // You can inject IHttpContextAccessor if needed
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("User activated: {UserId}", id);
                return ServiceResponse<bool>.CreateSuccess("User activated successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ActivateUserAsync for user {UserId}", id);
                return ServiceResponse<bool>.CreateError("An error occurred activating user");
            }
        }

        public async Task<ServiceResponse<bool>> DeactivateUserAsync(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return ServiceResponse<bool>.CreateError("User ID is required");

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return ServiceResponse<bool>.CreateError("User not found");

                if (!user.IsActive)
                    return ServiceResponse<bool>.CreateSuccess("User is already inactive", true);

                user.IsActive = false;
                user.DeactivatedAt = DateTime.UtcNow;
                var result = await _userManager.UpdateAsync(user);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<bool>.CreateError("Deactivation failed", errors);
                }

                // Log deactivation
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = id,
                    Action = "DEACTIVATE_USER",
                    EntityType = "User",
                    EntityId = id,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Unknown"
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("User deactivated: {UserId}", id);
                return ServiceResponse<bool>.CreateSuccess("User deactivated successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeactivateUserAsync for user {UserId}", id);
                return ServiceResponse<bool>.CreateError("An error occurred deactivating user");
            }
        }

        public async Task<ServiceResponse<bool>> UpdateUserRolesAsync(string id, List<string> roles)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return ServiceResponse<bool>.CreateError("User ID is required");

                if (roles == null || !roles.Any())
                    return ServiceResponse<bool>.CreateError("At least one role is required");

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return ServiceResponse<bool>.CreateError("User not found");

                // Validate that all roles exist in the system
                foreach (var role in roles)
                {
                    if (!await _roleManager.RoleExistsAsync(role))
                        return ServiceResponse<bool>.CreateError($"Role '{role}' does not exist in the system");
                }

                // Get current roles
                var currentRoles = await _userManager.GetRolesAsync(user);

                // Check if roles are actually changing
                if (currentRoles.OrderBy(r => r).SequenceEqual(roles.OrderBy(r => r)))
                    return ServiceResponse<bool>.CreateSuccess("Roles are already set as requested", true);

                // Remove all current roles
                if (currentRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeResult.Succeeded)
                    {
                        var errors = removeResult.Errors.Select(e => e.Description).ToList();
                        return ServiceResponse<bool>.CreateError("Failed to remove existing roles", errors);
                    }
                }

                // Add new roles
                var addResult = await _userManager.AddToRolesAsync(user, roles);
                if (!addResult.Succeeded)
                {
                    var errors = addResult.Errors.Select(e => e.Description).ToList();
                    return ServiceResponse<bool>.CreateError("Failed to add new roles", errors);
                }

                // Update user role field (primary role - take first)
                user.Role = roles.FirstOrDefault() ?? "User";
                user.UpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Log role update
                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = id,
                    Action = "UPDATE_ROLES",
                    EntityType = "User",
                    EntityId = id,
                    OldValues = string.Join(",", currentRoles),
                    NewValues = string.Join(",", roles),
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Unknown"
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("User roles updated for {UserId}: {NewRoles}", id, string.Join(", ", roles));
                return ServiceResponse<bool>.CreateSuccess("User roles updated successfully", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateUserRolesAsync for user {UserId}", id);
                return ServiceResponse<bool>.CreateError("An error occurred updating user roles");
            }
        }

        // Manual mapping methods to replace AutoMapper
        private UserResponseDto MapToUserResponseDto(ApplicationUser user)
        {
            if (user == null) return null;

            return new UserResponseDto
            {
                Id = user.Id,
                GhanaCardNumber = user.GhanaCardNumber,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                 
                Role = user.Role,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                UpdatedAt = user.UpdatedAt,
                Profile = user.Profile != null ? MapToUserProfileDto(user.Profile) : null
            };
        }

        private List<UserResponseDto> MapToUserResponseDtoList(List<ApplicationUser> users)
        {
            var result = new List<UserResponseDto>();
            foreach (var user in users)
            {
                result.Add(MapToUserResponseDto(user));
            }
            return result;
        }

        private UserProfileDto MapToUserProfileDto(UserProfile profile)
        {
            if (profile == null) return null;

            return new UserProfileDto
            {
                Address = profile.Address,
                City = profile.City,
                Region = profile.Region,
                PostalCode = profile.PostalCode,
                EmergencyContact = profile.EmergencyContact,
                EmergencyPhone = profile.EmergencyPhone,
                ProfilePictureUrl = profile.ProfilePictureUrl,
                LastUpdated = profile.LastUpdated
            };
        }
    }
}