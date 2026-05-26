using ChatApp.Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net.Mail;

namespace ChatApp.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendDeviceSwitchPinAsync(
            string toEmail,
            string displayName,
            string pin)
        {
            try
            {
                var apiKey = _config["SendGrid:ApiKey"];
                var fromEmail = _config["SendGrid:FromEmail"];
                var fromName = _config["SendGrid:FromName"];

                if (string.IsNullOrWhiteSpace(apiKey) ||
                    string.IsNullOrWhiteSpace(fromEmail))
                {
                    throw new Exception(
                        "SendGrid configuration missing.");
                }

                var client = new SendGridClient(apiKey);

                var from = new EmailAddress(
                    fromEmail,
                    fromName);

                var to = new EmailAddress(
                    toEmail,
                    displayName);

                var subject = "Your Device Switch PIN";

                var plainTextContent =
                    $"Hello {displayName}, Your verification PIN is: {pin}";

                var htmlContent = $@"
                    <h2>Device Switch Verification</h2>

                    <p>Hi {displayName},</p>

                    <p>Your verification PIN is:</p>

                    <h1>{pin}</h1>

                    <p>
                        This PIN will expire shortly.
                    </p>";

                var msg = MailHelper.CreateSingleEmail(
                    from,
                    to,
                    subject,
                    plainTextContent,
                    htmlContent);

                Console.WriteLine("Sending email...");

                var response = await client.SendEmailAsync(msg);

                Console.WriteLine(
                    $"Status Code: {response.StatusCode}");

                var responseBody =
                    await response.Body.ReadAsStringAsync();

                Console.WriteLine(responseBody);

                if ((int)response.StatusCode >= 400)
                {
                    throw new Exception(
                        $"SendGrid Error: {responseBody}");
                }

                Console.WriteLine(
                    "Email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("EMAIL ERROR:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);

                throw;
            }
        }
    }
}