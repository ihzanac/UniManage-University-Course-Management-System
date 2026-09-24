using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Data.Entity;
using System.Configuration;
using System.Threading.Tasks;
using Stripe.Checkout;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Services
{
    public interface IEmailService { Task SendAsync(string toEmail, string subject, string htmlBody); }
    public interface IOtpService
    {
        Task<string> GenerateForgotPasswordOtpAsync(ApplicationUser user);
        Task<bool> VerifyForgotPasswordOtpAsync(ApplicationUser user, string code);
    }
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSessionAsync(Course course, ApplicationUser user, string successUrl, string cancelUrl);
    }

    public sealed class SmtpEmailService : IEmailService
    {
        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return;
            }

            var host = ConfigurationManager.AppSettings["Smtp:Host"];
            var from = ResolveFromAddress();
            int port;
            if (!int.TryParse(ConfigurationManager.AppSettings["Smtp:Port"], out port))
            {
                port = 587;
            }

            // If SMTP is not configured yet, skip sending instead of crashing requests.
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                return;
            }

            using (var client = new SmtpClient(host, port))
            {
                var username = ConfigurationManager.AppSettings["Smtp:Username"];
                var password = ConfigurationManager.AppSettings["Smtp:Password"];
                if (!string.IsNullOrWhiteSpace(username))
                {
                    client.Credentials = new NetworkCredential(username, password);
                }

                client.EnableSsl = true;
                using (var message = new MailMessage(from, toEmail, subject, htmlBody) { IsBodyHtml = true })
                {
                    await client.SendMailAsync(message);
                }
            }
        }

        private static string ResolveFromAddress()
        {
            var configuredFrom = ConfigurationManager.AppSettings["Smtp:From"];
            if (!string.IsNullOrWhiteSpace(configuredFrom))
            {
                return configuredFrom;
            }

            var username = ConfigurationManager.AppSettings["Smtp:Username"];
            return string.IsNullOrWhiteSpace(username) ? string.Empty : username;
        }
    }

    public sealed class OtpService : IOtpService
    {
        private readonly ApplicationDbContext _dbContext = ApplicationDbContext.Create();

        public async Task<string> GenerateForgotPasswordOtpAsync(ApplicationUser user)
        {
            var otp = new Random().Next(100000, 999999).ToString();
            _dbContext.OtpTokens.Add(new OtpToken { UserId = user.Id, Code = otp, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) });
            await _dbContext.SaveChangesAsync();
            return otp;
        }

        public async Task<bool> VerifyForgotPasswordOtpAsync(ApplicationUser user, string code)
        {
            var token = await _dbContext.OtpTokens.OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Code == code && !t.IsUsed);
            if (token == null || token.ExpiresAtUtc < DateTime.UtcNow) return false;
            token.IsUsed = true;
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }

    public sealed class StripePaymentService : IPaymentService
    {
        public Task<string> CreateCheckoutSessionAsync(Course course, ApplicationUser user, string successUrl, string cancelUrl)
        {
            Stripe.StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["Stripe:SecretKey"];
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "lkr",
                            UnitAmount = (long)(course.Price * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = course.Title }
                        }
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "CourseId", course.Id.ToString() },
                    { "UserId", user.Id }
                }
            };
            return Task.FromResult(new SessionService().Create(options).Url);
        }
    }
}
