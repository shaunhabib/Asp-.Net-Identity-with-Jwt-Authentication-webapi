using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Asp.Net_Identity.ViewModels
{
    public class RefreshTokenVm
    {
        [Required]
        public string RefreshToken { get; set; }
    }
}