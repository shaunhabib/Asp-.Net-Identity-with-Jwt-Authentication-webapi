using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace Asp.Net_Identity.Service
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string SendEmail(string ToMail, string subject, string content)
        {
            using var smtp = new SmtpClient();
            try
            {
                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(_configuration["EmailConfig:HostEmail"]));
                message.To.Add(MailboxAddress.Parse(ToMail));
                message.Subject = subject;
                message.Body = new TextPart(TextFormat.Html)
                {
                    Text = content
                };

                smtp.Connect(_configuration["EmailConfig:Host"], Convert.ToInt32(_configuration["EmailConfig:Port"]), SecureSocketOptions.StartTls);
                smtp.Authenticate(_configuration["EmailConfig:HostEmail"], _configuration["EmailConfig:HostEmailPassword"]);
                smtp.Send(message);
                smtp.Disconnect(true);
                smtp.Dispose();
                return "Successfully sent";
            }
            catch (System.Exception ex)
            {
                smtp.Disconnect(true);
                smtp.Dispose();
                return ex.Message;
            }
            
        }
    }
}