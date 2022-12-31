using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Asp.Net_Identity.Service;
using Asp.Net_Identity.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Asp.Net_Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class RegisterController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _mailService;

        public RegisterController(UserManager<IdentityUser> userManager, IConfiguration configuration, IEmailService mailService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _mailService = mailService;
        }
        
        [HttpPost]
        public async Task<IActionResult> Register([FromBody]RegisterVm register)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid model state");

            #region creating user
            var user = new IdentityUser
            {
                UserName = register.UserName,
                Email = register.Email
            };
            var result = await _userManager.CreateAsync(user, register.Password);
            #endregion
            
            if(!result.Succeeded)
                BadRequest("Failed to register");

            await SendConfirmationEmail(user);
            return Ok("Successfully registered! Please confirm your email");
        }


        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if(string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(token))
                return NotFound("Invalid userId and token");

            var user = await _userManager.FindByIdAsync(userId);
            if(user is null)
                return NotFound("No user found with this id");

            //Decripting token
            var decodedToken = WebEncoders.Base64UrlDecode(token);
            string normalToken = Encoding.UTF8.GetString(decodedToken);

            var result = await _userManager.ConfirmEmailAsync(user, normalToken);
            return result.Succeeded == true 
                ? Ok("Your email has been confirmed successfully")
                : BadRequest("Failed to confirm your email");
        }

        private async Task SendConfirmationEmail(IdentityUser user)
        {
            //Generating email confirmation token
            var confirmEmailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            //Encripting token
            var encodedEmailToken = Encoding.UTF8.GetBytes(confirmEmailToken);
            var validEmailToken = WebEncoders.Base64UrlEncode(encodedEmailToken);

            string url = $"{_configuration["AppUrl"]}/api/Register/ConfirmEmail?userid={user.Id}&token={validEmailToken}";

            string body = "<h3>Welcome to our website</h3>"+
            $"<p>Confirm your email by <a href='{url}'>clicking here</a>";

            //sending mail via mail service
            var sent = _mailService.SendEmail(user.Email, "Confirm email", body);
        }
    }
}