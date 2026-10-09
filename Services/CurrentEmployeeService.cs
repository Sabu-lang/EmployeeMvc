using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Services
{
    public interface ICurrentEmployeeService
    {
        Task<Employee?> GetAsync(ClaimsPrincipal principal);
    }


    public class CurrentEmployeeService : ICurrentEmployeeService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CurrentEmployeeService(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<Employee?> GetAsync(ClaimsPrincipal principal)
        {
            var user = await _userManager.GetUserAsync(principal);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return null;
            }



            var linkedEmployee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.AccountId == user.Id);

            if (linkedEmployee != null) return linkedEmployee;
            if (!user.EmailConfirmed) return null;

            var email = user.Email.ToLower();
            return await _context.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email.ToLower() == email);
        }
    }
}
