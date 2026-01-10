using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RentControlSystem.API.Data;
using RentControlSystem.Auth.API.Data;
using RentControlSystem.Auth.API.Models;

namespace RentControlSystem.Auth.API.Services
{
    public class EmailBackgroundService : BackgroundService
    {
        private readonly ILogger<EmailBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public EmailBackgroundService(ILogger<EmailBackgroundService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                        // Process pending emails from database
                        var pendingEmails = await dbContext.EmailQueue
                            .Where(e => !e.IsSent && e.RetryCount < 3)
                            .OrderBy(e => e.CreatedAt)
                            .Take(10)
                            .ToListAsync(stoppingToken);

                        foreach (var email in pendingEmails)
                        {
                            try
                            {
                                var success = await emailService.SendEmailAsync(
                                    email.ToEmail,
                                    email.Subject,
                                    email.Body,
                                    email.IsHtml);

                                if (success)
                                {
                                    email.IsSent = true;
                                    email.SentAt = DateTime.UtcNow;
                                    _logger.LogInformation("Email sent successfully to {ToEmail}", email.ToEmail);
                                }
                                else
                                {
                                    email.RetryCount++;
                                    email.LastError = "Failed to send email";
                                    _logger.LogWarning("Failed to send email to {ToEmail}, retry {RetryCount}",
                                        email.ToEmail, email.RetryCount);
                                }
                            }
                            catch (Exception ex)
                            {
                                email.RetryCount++;
                                email.LastError = ex.Message;
                                _logger.LogError(ex, "Error sending email to {ToEmail}", email.ToEmail);
                            }

                            email.LastAttempt = DateTime.UtcNow;
                        }

                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Email Background Service");
                }

                // Wait for 30 seconds before next check
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            _logger.LogInformation("Email Background Service is stopping.");
        }
    }
}