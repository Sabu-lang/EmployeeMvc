using System.Security.Claims;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Services
{
    /// <summary>Limits Admin queries to employees in groups owned by that Admin.</summary>
    public static class EmployeeScopeExtensions
    {
        public static IQueryable<Employee> ScopeToAdminGroups(
            this IQueryable<Employee> employees,
            ApplicationDbContext context,
            ClaimsPrincipal principal)
        {
            if (!principal.IsInRole(AppRoles.Admin)) return employees;

            var ownerId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(ownerId)) return employees.Where(_ => false);

            var memberUserIds = context.GroupMembers
                .Where(m => context.Groups.Any(g => g.Id == m.GroupId && g.OwnerId == ownerId))
                .Select(m => m.UserId);

            return employees.Where(e =>
                (e.AccountId != null && memberUserIds.Contains(e.AccountId)) ||
                context.Users.Any(u => memberUserIds.Contains(u.Id) &&
                    u.Email != null && u.Email.ToLower() == e.Email.ToLower()));
        }
    }
}
