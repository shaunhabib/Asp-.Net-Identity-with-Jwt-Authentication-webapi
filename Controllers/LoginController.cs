using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Asp.Net_Identity.Service;
using Asp.Net_Identity.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace Asp.Net_Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class LoginController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _mailService;

        public LoginController(UserManager<IdentityUser> userManager, IConfiguration configuration, IEmailService mailService)
        {
            _userManager = userManager;
            _configuration = configuration;
            _mailService = mailService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Hello");
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login([FromBody]LoginVm login)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid model state");

            var user = await _userManager.FindByNameAsync(login.UserName);

            if(user is null)
                return BadRequest("No user found with this user name");
            
            var result = await _userManager.CheckPasswordAsync(user, login.Password);

            if(!result)
                return BadRequest("Password is incorrect");
            
            return Ok(GenerateToken(user));
        }


        [HttpPost]
        public async Task<IActionResult> ForgetPassword(string email)
        {
            if(string.IsNullOrWhiteSpace(email))
                return NotFound("Invalid email");
            
            bool result = await ForgetPasswordAsync(email);

            if(!result)
                return BadRequest("Something went wrong");
            
            return Ok("Reset password token has been sent. Please check your email");
        }


        private string GenerateToken(IdentityUser user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credientials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserName", user.UserName)
            };

            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credientials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<bool> ForgetPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if(user is null)
              return false;
            
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Encoding.UTF8.GetBytes(token);
            var validToken = WebEncoders.Base64UrlEncode(encodedToken);

            string url = $"{_configuration["AppUrl"]}/api/Login/ResetPassword?email={email}&token={validToken}";

            string body = "<h3>Welcome dude</h3>"+
            $"<p>Reset your password by <a href='{url}'>clicking here</a>";

            //sending mail via mail service
            var sent = _mailService.SendEmail(user.Email, "Reset Passsword", body); 
            return true;  
        }


    }
}