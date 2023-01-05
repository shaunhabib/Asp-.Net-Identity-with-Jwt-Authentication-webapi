using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Asp.Net_Identity.DataContext;
using Asp.Net_Identity.Models;
using Asp.Net_Identity.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Asp.Net_Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Authorize]
    public class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmployeeController(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet]
        public IActionResult GetCurrentUser()
        {
            string UserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string email = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
            string userName = _httpContextAccessor.HttpContext?.User?.FindFirst("UserName")?.Value;
            string Token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"];

            var result = new UserInfo
            {
                UserId = UserId,
                UserName = userName,
                Email = email,
                Token = Token
            };

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var emplist = await _context.Employees.AsNoTracking().ToListAsync();
            return Ok(emplist);
        }

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return emp != null ? Ok(emp) : NotFound("No employee found with this id");
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrUpdate([FromBody] CreateOrUpdateEmpVm command)
        {
            try
            {
                if(!ModelState.IsValid)
                    return BadRequest("Some properties are not invalid");

                #region Create employee
                if (command.Id == 0)
                {
                    var emp = new Employees
                    {
                        Name = command.Name,
                        PhoneNumber = command.PhoneNumber,
                        Designation = command.Designation,
                        Salary = command.Salary
                    };
                    await _context.Employees.AddAsync(emp);
                }
                #endregion
                #region Update employee
                else
                {
                    var exEmp = await _context.Employees.FirstOrDefaultAsync(x => x.Id == command.Id);
                    if(exEmp is null)
                        return NotFound("No employee found with this id");
                    
                    exEmp.Name = command.Name;
                    exEmp.PhoneNumber = command.PhoneNumber;
                    exEmp.Designation = command.Designation;
                    exEmp.Salary = command.Salary;
                }
                #endregion

                await _context.SaveChangesAsync();
                return Ok("Successfully created");
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}