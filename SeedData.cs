using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentControlSystem.API.Data;
using RentControlSystem.Auth.API.Models;

namespace RentControlSystem.Auth.API.Data
{
    public static class SeedData
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get logger without generic type parameter for static class
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

            try
            {
                // Ensure database exists and is migrated
                await context.Database.EnsureCreatedAsync();

                // Seed roles
                var roles = new[] { "Admin", "RCD_Officer", "Inspector", "Landlord", "Tenant" };

                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                // Seed admin user
                var adminEmail = "admin@rentcontrol.gov.gh";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = "GHA-000000001-1",
                        Email = adminEmail,
                        GhanaCardNumber = "GHA-000000001-1",
                        FirstName = "System",
                        LastName = "Administrator",
                        PhoneNumber = "+233244000000",
                        DateOfBirth = new DateTime(1980, 1, 1),
                        Role = "Admin",
                        IsActive = true,
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin@123456");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "Admin");

                        // Create admin profile
                        var profile = new UserProfile
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserId = adminUser.Id,
                            Address = "Rent Control Department Headquarters",
                            City = "Accra",
                            Region = "Greater Accra",
                            EmergencyContact = "Administration Office",
                            EmergencyPhone = "+233244000001",
                            CreatedAt = DateTime.UtcNow
                        };

                        context.UserProfiles.Add(profile);
                        await context.SaveChangesAsync();

                        logger.LogInformation("Successfully created admin user and profile.");
                    }
                    else
                    {
                        // Log errors if user creation fails
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        logger.LogError("Failed to create admin user: {Errors}", errors);
                    }
                }
                else
                {
                    logger.LogInformation("Admin user already exists.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
    }
}