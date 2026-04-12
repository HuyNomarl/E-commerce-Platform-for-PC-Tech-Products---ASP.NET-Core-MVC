using Eshop.Models.Configurations;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Eshop.Areas.Admin.Repository
{
    public class EmailSender : IEmailSender
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailSender(IOptions<SmtpSettings> smtpOptions)
        {
            _smtpSettings = smtpOptions.Value;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(_smtpSettings.Host) ||
                string.IsNullOrWhiteSpace(_smtpSettings.SenderEmail) ||
                string.IsNullOrWhiteSpace(_smtpSettings.UserName) ||
                string.IsNullOrWhiteSpace(_smtpSettings.Password))
            {
                throw new InvalidOperationException("Thiếu cấu hình SMTP. Hãy kiểm tra section Smtp trong cấu hình bí mật.");
            }

            var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
            {
                EnableSsl = _smtpSettings.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpSettings.UserName, _smtpSettings.Password),
            };

            var mail = new MailMessage
            {
                From = new MailAddress(_smtpSettings.SenderEmail, _smtpSettings.SenderName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            mail.To.Add(email);

            return client.SendMailAsync(mail);
        }
    }
}
