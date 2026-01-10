using RentControlSystem.Auth.API.DTOs;

namespace RentControlSystem.Auth.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
        Task<bool> SendEmailVerificationAsync(string toEmail, string verificationLink);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
        Task<bool> SendWelcomeEmailAsync(string toEmail, string name);
        Task<bool> SendAccountDeactivatedEmailAsync(string toEmail, string name);
        Task<bool> SendAccountActivatedEmailAsync(string toEmail, string name);
        Task<bool> SendPasswordChangedEmailAsync(string toEmail, string name);
    }
}