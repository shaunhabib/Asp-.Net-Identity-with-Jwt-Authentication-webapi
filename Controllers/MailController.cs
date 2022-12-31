using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Net_Identity.Service;
using Microsoft.AspNetCore.Mvc;

namespace Asp.Net_Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class MailController : ControllerBase
    {
        private readonly IEmailService _mailService;

        public MailController(IEmailService mailService)
        {
            _mailService = mailService;
        }

        [HttpGet]
        public IActionResult SendMail(string ToMail, string subject, string content)
        {
            return Ok(_mailService.SendEmail(ToMail, subject, content));
        }
    }
}