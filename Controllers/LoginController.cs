using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Asp.Net_Identity.DataContext;
using Asp.Net_Identity.Models;
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
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoginController(UserManager<IdentityUser> userManager, IConfiguration configuration, IEmailService mailService, AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _configuration = configuration;
            _mailService = mailService;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
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
            
            var token = await GenerateToken(user);
            string refReshToken = await GenerateRefreshToken(user.Id, token.JwtId);

            return Ok(new AuthResponse
            {
                Token = token.Token,
                RefreshToken = refReshToken
            });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            string UserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if(String.IsNullOrWhiteSpace(UserId))
                return Unauthorized();
            
            var refreshTokens = _context.RefreshToken.Where(x => x.UserId == UserId).ToList();
            if(refreshTokens is null || refreshTokens.Count == 0)
                return Unauthorized();

            _context.RefreshToken.RemoveRange(refreshTokens);
            await _context.SaveChangesAsync();

            return Ok("Successfully logged out");
        }

        [HttpPost]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenVm Vm)
        {
            if(!ModelState.IsValid)
                return BadRequest("Invalid modelstate");

            string UserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string JwtId = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            var refreshToken = _context.RefreshToken.FirstOrDefault(x => x.Token.Equals(Vm.RefreshToken) && x.UserId.Equals(UserId) && x.JwtId.Equals(JwtId));

            if(refreshToken is null)
                return BadRequest("Invalid token");
            
            if(refreshToken.ExpireDate < DateTime.Now)
                return BadRequest("Token has expired!");
            
            var user = await _userManager.FindByIdAsync(UserId);
            if(user is null)
                return BadRequest("User not found");

            var newToken = await GenerateToken(user);
            refreshToken.Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            refreshToken.ExpireDate = DateTime.Now.AddHours(6);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponse
            {
                Token = newToken.Token,
                RefreshToken = refreshToken.Token
            });
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

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string token, [FromBody]ResetPasswordVm model)
        {
            if(!ModelState.IsValid)
                return BadRequest("Model state is invalid");

            var result = await ResetPasswordAsync(email, token, model);

            if(result) 
                return Ok("Password has been reset successfully");

            return BadRequest("Failed to reset password");
        }



        
        private async Task<(string Token, string JwtId)> GenerateToken(IdentityUser user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credientials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("UserName", user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddMinutes(2),
                signingCredentials: credientials
            );
            var Token = new JwtSecurityTokenHandler().WriteToken(token);
            return(Token, token.Id);
        }

        private async Task<string> GenerateRefreshToken(string UserId, string jwtId)
        {
            #region  RefreshToken
            var refreshToken = new RefreshToken
            {
                UserId = UserId,
                JwtId = jwtId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                CreationDate = DateTime.Now,
                ExpireDate = DateTime.Now.AddHours(6)
            };
            await _context.RefreshToken.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
            #endregion

            return refreshToken.Token;
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

        private async Task<bool> ResetPasswordAsync(string email, string token ,ResetPasswordVm model)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if(user is null)
              return false;

            //Decripting token
            var decodedToken = WebEncoders.Base64UrlDecode(token);
            string normalToken = Encoding.UTF8.GetString(decodedToken);

            var result = await _userManager.ResetPasswordAsync(user, normalToken, model.NewPassword);
            if(result.Succeeded)
                return true;

            return false;
        }
    }
}