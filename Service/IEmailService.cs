using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Asp.Net_Identity.Service
{
    public interface IEmailService
    {
        string SendEmail(string ToMail, string subject, string content);
    }
}