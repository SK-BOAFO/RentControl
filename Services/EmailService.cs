using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentControlSystem.Auth.API.DTOs;

namespace RentControlSystem.Auth.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly SmtpClient _smtpClient;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Configure SMTP client
            _smtpClient = new SmtpClient
            {
                Host = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com",
                Port = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"),
                EnableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _configuration["EmailSettings:Username"],
                    _configuration["EmailSettings:Password"]
                )
            };
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            try
            {
                if (string.IsNullOrEmpty(toEmail))
                    throw new ArgumentException("Recipient email cannot be empty");

                var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@rentcontrolsystem.com";
                var fromName = _configuration["EmailSettings:FromName"] ?? "Rent Control System";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml,
                    Priority = MailPriority.Normal
                };

                mailMessage.To.Add(toEmail);

                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendEmailVerificationAsync(string toEmail, string verificationLink)
        {
            try
            {
                var subject = "Verify Your Email - Rent Control System";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 30px; background-color: #f8f9fa; }}
                            .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; 
                                      color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Email Verification</h1>
                            </div>
                            <div class='content'>
                                <h2>Hello,</h2>
                                <p>Thank you for registering with the Ghana Rent Control System. Please verify your email address by clicking the button below:</p>
                                <p style='text-align: center;'>
                                    <a href='{verificationLink}' class='button'>Verify Email Address</a>
                                </p>
                                <p>If the button doesn't work, you can also copy and paste this link into your browser:</p>
                                <p style='word-break: break-all; color: #007bff;'>{verificationLink}</p>
                                <p>This verification link will expire in 24 hours.</p>
                                <p>If you didn't create an account, please ignore this email.</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Ghana Rent Control System. All rights reserved.</p>
                                <p>This is an automated email, please do not reply.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email verification to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            try
            {
                var subject = "Password Reset Request - Rent Control System";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 30px; background-color: #f8f9fa; }}
                            .button {{ display: inline-block; padding: 12px 24px; background-color: #dc3545; 
                                      color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                            .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; 
                                       border-radius: 5px; margin: 20px 0; color: #856404; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Password Reset</h1>
                            </div>
                            <div class='content'>
                                <h2>Hello,</h2>
                                <p>We received a request to reset your password for the Ghana Rent Control System account.</p>
                                <p style='text-align: center;'>
                                    <a href='{resetLink}' class='button'>Reset Password</a>
                                </p>
                                <p>If the button doesn't work, you can also copy and paste this link into your browser:</p>
                                <p style='word-break: break-all; color: #dc3545;'>{resetLink}</p>
                                <div class='warning'>
                                    <p><strong>Important:</strong> This password reset link will expire in 15 minutes.</p>
                                    <p>If you did not request a password reset, please ignore this email or contact our support team immediately.</p>
                                </div>
                                <p>For security reasons, we recommend that you:</p>
                                <ul>
                                    <li>Use a strong, unique password</li>
                                    <li>Never share your password with anyone</li>
                                    <li>Change your password regularly</li>
                                </ul>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Ghana Rent Control System. All rights reserved.</p>
                                <p>This is an automated email, please do not reply.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(string toEmail, string name)
        {
            try
            {
                var subject = "Welcome to Rent Control System";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 30px; background-color: #f8f9fa; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                            .feature {{ background-color: white; padding: 15px; margin: 10px 0; border-radius: 5px; 
                                       border-left: 4px solid #28a745; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Welcome Aboard!</h1>
                            </div>
                            <div class='content'>
                                <h2>Hello {name},</h2>
                                <p>Welcome to the Ghana Rent Control System! We're excited to have you join our platform.</p>
                                
                                <div class='feature'>
                                    <h3>🏠 Get Started</h3>
                                    <p>Log in to your account to start managing your properties or finding your perfect home.</p>
                                </div>
                                
                                <div class='feature'>
                                    <h3>📱 Mobile Friendly</h3>
                                    <p>Access our platform from any device - desktop, tablet, or mobile phone.</p>
                                </div>
                                
                                <div class='feature'>
                                    <h3>🛡️ Secure & Reliable</h3>
                                    <p>Your data is protected with enterprise-grade security measures.</p>
                                </div>
                                
                                <p>If you have any questions or need assistance, please don't hesitate to contact our support team.</p>
                                
                                <p>Best regards,<br>The Rent Control System Team</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Ghana Rent Control System. All rights reserved.</p>
                                <p>This is an automated email, please do not reply.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending welcome email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendAccountDeactivatedEmailAsync(string toEmail, string name)
        {
            try
            {
                var subject = "Account Deactivated - Rent Control System";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #6c757d; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 30px; background-color: #f8f9fa; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                            .info {{ background-color: #e9ecef; padding: 15px; border-radius: 5px; margin: 20px 0; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Account Deactivated</h1>
                            </div>
                            <div class='content'>
                                <h2>Hello {name},</h2>
                                <p>Your account with the Ghana Rent Control System has been deactivated.</p>
                                
                                <div class='info'>
                                    <p><strong>Account Details:</strong></p>
                                    <p>Email: {toEmail}</p>
                                    <p>Deactivation Date: {DateTime.Now.ToString("dd MMM yyyy")}</p>
                                </div>
                                
                                <p>If you believe this was done in error, or if you wish to reactivate your account, please contact our support team.</p>
                                
                                <p>Thank you for using the Rent Control System.</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Ghana Rent Control System. All rights reserved.</p>
                                <p>This is an automated email, please do not reply.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending account deactivation email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendAccountActivatedEmailAsync(string toEmail, string name)
        {
            try
            {
                var subject = "Account Activated - Rent Control System";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #28a745; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 30px; background-color: #f8f9fa; }}
                            .button {{ display: inline-block; padding: 12px 24px; background-color: #28a745; 
                                      color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Account Activated</h1>
                            </div>
                            <div class='content'>
                                <h2>Hello {name},</h2>
                                <p>Your account with the Ghana Rent Control System has been successfully activated!</p>
                                
                                <p>You can now log in to your account and access all the features of our platform.</p>
                                
                                <p style='text-align: center;'>
                                    <a href='{_configuration["ClientApp:Url"]}/login' class='button'>Log In to Your Account</a>
                                </p>
                                
                                <p>If you have any questions or need assistance, our support team is here to help.</p>
                                
                                <p>Welcome back to the Rent Control System!</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Ghana Rent Control System. All rights reserved.</p>
                                <p>This is an automated email, please do not reply.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending account activation email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendPasswordChangedEmailAsync(string toEmail, string name)
        {
            try
            {
                var subject = "Password Changed Successfully - Rent Control System";
                var body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #17a2b8; color: white; padding: 20px; text-align: center; }}
                            .content {{ padding: 30px; background-color: #f8f9fa; }}
                            .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
                            .security-tips {{ background-color: #d1ecf1; padding: 15px; border-radius: 5px; 
                                             border-left: 4px solid #17a2b8; margin: 20px 0; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Password Updated</h1>
                            </div>
                            <div class='content'>
                                <h2>Hello {name},</h2>
                                <p>Your password for the Ghana Rent Control System has been successfully changed.</p>
                                
                                <div class='security-tips'>
                                    <h3>🔒 Security Tips:</h3>
                                    <ul>
                                        <li>Use a unique password for each online account</li>
                                        <li>Never share your password with anyone</li>
                                        <li>Enable two-factor authentication if available</li>
                                        <li>Regularly update your password</li>
                                        <li>Use a password manager to store your passwords securely</li>
                                    </ul>
                                </div>
                                
                                <p><strong>Important:</strong> If you did not request this password change, please contact our support team immediately.</p>
                                
                                <p>For your security, we recommend logging out of all devices and logging back in with your new password.</p>
                            </div>
                            <div class='footer'>
                                <p>&copy; {DateTime.Now.Year} Ghana Rent Control System. All rights reserved.</p>
                                <p>This is an automated email, please do not reply.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                return await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password changed email to {ToEmail}", toEmail);
                return false;
            }
        }
    }
}