using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace EmployeeManagementSystem.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var smtpServer = _config["EmailSettings:SmtpServer"];
            var port = int.Parse(_config["EmailSettings:SmtpPort"]);
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var password = _config["EmailSettings:SenderPassword"];
            var enableSsl = bool.Parse(_config["EmailSettings:EnableSsl"]);

            using var client = new SmtpClient(smtpServer, port);
            client.EnableSsl = enableSsl;
            client.Credentials = new NetworkCredential(senderEmail, password);

            var message = new MailMessage(senderEmail, to, subject, body);
            message.IsBodyHtml = true;

            await client.SendMailAsync(message);
        }
    }
}