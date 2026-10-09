using System.Security.Claims;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Services
{

    public static class EmployeeScopeExtensions
    {
        public static IQueryable<Employee> ScopeToVisibleGroups(
            this IQueryable<Employee> employees,
            ApplicationDbContext context,
            ClaimsPrincipal principal)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return employees.Where(_ => false);

            var groups = context.Groups.AsQueryable();
            if (principal.IsInRole(AppRoles.Admin))
            {

                groups = groups.Where(g => g.OwnerId == userId);
            }
            else
            {

                groups = groups.Where(g => g.OwnerId == userId ||
                    g.Members.Any(m => m.UserId == userId));
            }

            var groupIds = groups.Select(g => g.Id);
            var visibleUserIds = context.GroupMembers
                .Where(m => groupIds.Contains(m.GroupId))
                .Select(m => m.UserId)
                .Union(context.Groups
                    .Where(g => groupIds.Contains(g.Id))
                    .Select(g => g.OwnerId));

            return employees.Where(e =>
                (e.AccountId != null && visibleUserIds.Contains(e.AccountId)) ||
                context.Users.Any(u => visibleUserIds.Contains(u.Id) &&
                    u.Email != null && u.Email.ToLower() == e.Email.ToLower()));
        }
    }
}
