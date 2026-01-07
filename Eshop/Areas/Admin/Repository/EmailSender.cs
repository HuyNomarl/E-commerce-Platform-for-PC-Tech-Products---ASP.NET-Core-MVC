using System.Net;
using System.Net.Mail;

namespace Eshop.Areas.Admin.Repository
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("techshopvn365@gmail.com", "gcoofuqidbilnktm"),
            };

            var mail = new MailMessage();
            mail.From = new MailAddress("techshopvn365@gmail.com", "Eshop");
            mail.To.Add(email);
            mail.Subject = subject;
            mail.Body = htmlMessage;
            mail.IsBodyHtml = true;

            return client.SendMailAsync(mail);
        }
    }
}
