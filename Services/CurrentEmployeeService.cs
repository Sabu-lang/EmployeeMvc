using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Services
{
    public interface ICurrentEmployeeService
    {
        /// <summary>აბრუნებს მიმდინარე მომხმარებლის Employee ჩანაწერს (ან null-ს).</summary>
        Task<Employee?> GetAsync(ClaimsPrincipal principal);
    }

    /// <summary>
    /// Identity მომხმარებელი ↔ Employee ჩანაწერი ერთმანეთს უკავშირდება ელფოსტით.
    /// უსაფრთხოებისთვის საჭიროა, რომ მომხმარებლის ელფოსტა დადასტურებული იყოს
    /// (ე.ი. მან მართლა აკონტროლებს ამ მისამართს).
    /// </summary>
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
            if (user == null || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
            {
                return null;
            }

            var email = user.Email.ToLower();
            return await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Email.ToLower() == email);
        }
    }
}
