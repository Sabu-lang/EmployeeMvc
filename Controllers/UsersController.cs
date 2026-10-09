using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{

    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public UsersController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string? email)
        {
            var currentUserId = _userManager.GetUserId(User);
            var searchEmail = (email ?? string.Empty).Trim();
            var ownedGroupMemberIds = await _context.GroupMembers
                .Where(m => _context.Groups.Any(g => g.Id == m.GroupId && g.OwnerId == currentUserId))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();
            var anyGroupMemberIds = await _context.GroupMembers
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();
            var otherGroupMemberIds = await _context.GroupMembers
                .Where(m => !_context.Groups.Any(g => g.Id == m.GroupId && g.OwnerId == currentUserId))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();
            var assignableRoleIds = _context.Roles
                .Where(r => r.Name == AppRoles.Employee || r.Name == AppRoles.Manager || r.Name == AppRoles.Support)
                .Select(r => r.Id);
            var adminRoleIds = _context.Roles
                .Where(r => r.Name == AppRoles.Admin)
                .Select(r => r.Id);

            var usersQuery = _userManager.Users.AsQueryable();
            if (searchEmail.Length > 0)
            {
                var normalizedSearch = searchEmail.ToLowerInvariant();
                usersQuery = usersQuery.Where(u => u.Email != null &&
                    u.Email.ToLower().Contains(normalizedSearch) &&
                    ((ownedGroupMemberIds.Contains(u.Id) && !otherGroupMemberIds.Contains(u.Id)) || !anyGroupMemberIds.Contains(u.Id)) &&
                    _context.UserRoles.Any(ur => ur.UserId == u.Id && assignableRoleIds.Contains(ur.RoleId)) &&
                    !_context.UserRoles.Any(ur => ur.UserId == u.Id && adminRoleIds.Contains(ur.RoleId)));
            }
            else
            {
                usersQuery = usersQuery.Where(u => u.Id == currentUserId ||
                    (ownedGroupMemberIds.Contains(u.Id) && !otherGroupMemberIds.Contains(u.Id)));
            }

            var users = await usersQuery
                .OrderBy(u => u.Email)
                .ToListAsync();
            ViewData["SearchEmail"] = searchEmail;

            var roleRows = await (from ur in _context.UserRoles
                                  join r in _context.Roles on ur.RoleId equals r.Id
                                  select new { ur.UserId, RoleName = r.Name })
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var model = users.Select(u => new UserListItemViewModel
            {
                Id = u.Id,
                Email = u.Email ?? u.UserName ?? u.Id,
                EmailConfirmed = u.EmailConfirmed,
                TwoFactorEnabled = u.TwoFactorEnabled,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > now,
                IsCurrentUser = u.Id == currentUserId,
                Roles = roleRows.Where(r => r.UserId == u.Id && r.RoleName != null).Select(r => r.RoleName!).OrderBy(n => n).ToList(),
                CanManageRoles = u.Id != currentUserId &&
                    roleRows.Any(r => r.UserId == u.Id &&
                        (r.RoleName == AppRoles.Employee || r.RoleName == AppRoles.Manager || r.RoleName == AppRoles.Support)) &&
                    !roleRows.Any(r => r.UserId == u.Id && r.RoleName == AppRoles.Admin) &&
                    ((ownedGroupMemberIds.Contains(u.Id) && !otherGroupMemberIds.Contains(u.Id)) ||
                     (!anyGroupMemberIds.Contains(u.Id) && u.EmailConfirmed)),
                CanDelete = u.Id != currentUserId && ownedGroupMemberIds.Contains(u.Id) &&
                    !otherGroupMemberIds.Contains(u.Id) &&
                    roleRows.Any(r => r.UserId == u.Id && r.RoleName == AppRoles.Employee)
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Roles(string? id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var isSelf = user.Id == _userManager.GetUserId(User);
            if (!isSelf && !await CanManageRolesAsync(user)) return Forbid();

            var canGrantAdmin = !isSelf && await CanGrantAdminAsync(user);
            return View(await BuildRolesModelAsync(user, isSelf, canGrantAdmin));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Roles(string id, string[]? selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var isSelf = user.Id == _userManager.GetUserId(User);
            if (!isSelf && !await CanManageRolesAsync(user)) return Forbid();

            var canGrantAdmin = !isSelf && await CanGrantAdminAsync(user);
            var allowedRoles = isSelf
                ? AppRoles.All
                : canGrantAdmin
                    ? AppRoles.All
                    : new[] { AppRoles.Employee, AppRoles.Manager, AppRoles.Support };
            var desired = (selectedRoles ?? Array.Empty<string>())
                .Where(r => allowedRoles.Contains(r))
                .Distinct()
                .ToList();

            if (desired.Count == 0 || (!isSelf && desired.Count != 1))
            {
                TempData["UserError"] = isSelf
                    ? "მომხმარებელს მინიმუმ ერთი როლი უნდა ჰქონდეს."
                    : "აირჩიეთ ზუსტად ერთი როლი.";
                return RedirectToAction(nameof(Roles), new { id });
            }

            if (!isSelf)
            {

                desired = new List<string> { desired[0] };
            }


            if (user.Id == _userManager.GetUserId(User) && !desired.Contains(AppRoles.Admin))
            {
                TempData["UserError"] = "საკუთარ თავს Admin როლს ვერ ჩამოაშორებთ.";
                return RedirectToAction(nameof(Roles), new { id });
            }

            var current = await _userManager.GetRolesAsync(user);

            var toAdd = desired.Except(current).ToList();
            var toRemove = current.Except(desired).ToList();


            if (toAdd.Count > 0)
            {
                var added = await _userManager.AddToRolesAsync(user, toAdd);
                if (!added.Succeeded)
                {
                    TempData["UserError"] = string.Join(" ", added.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Roles), new { id });
                }
            }

            if (toRemove.Count > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                {
                    TempData["UserError"] = string.Join(" ", removed.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Roles), new { id });
                }
            }


            await _userManager.UpdateSecurityStampAsync(user);


            if (user.Id == _userManager.GetUserId(User))
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            TempData["UserSuccess"] = $"{user.Email}-ის როლები განახლდა.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["UserError"] = "საკუთარი ანგარიშის წაშლა შეუძლებელია.";
                return RedirectToAction(nameof(Index));
            }

            if (!await IsManagedEmployeeAsync(user.Id)) return Forbid();

            if (await _context.Groups.AnyAsync(g => g.OwnerId == user.Id))
            {
                TempData["UserError"] = "მომხმარებელი ჯგუფების მფლობელია. ჯერ შეცვალეთ ამ ჯგუფების მფლობელი ან წაშალეთ ისინი.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user); 
            if (result.Succeeded)
            {
                var linkedEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.AccountId == user.Id);
                if (linkedEmployee != null)
                {
                    linkedEmployee.AccountId = null;
                    await _context.SaveChangesAsync();
                }
            }

            TempData[result.Succeeded ? "UserSuccess" : "UserError"] = result.Succeeded
                ? "მომხმარებელი წაიშალა."
                : string.Join(" ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        private async Task<UserRolesViewModel> BuildRolesModelAsync(IdentityUser user, bool isSelf, bool canGrantAdmin)
        {
            var currentRoles = (await _userManager.GetRolesAsync(user)).ToList();
            var assignableRoles = isSelf || canGrantAdmin
                ? AppRoles.All.ToList()
                : new List<string> { AppRoles.Employee, AppRoles.Manager, AppRoles.Support };
            return new UserRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? user.Id,
                IsSelf = isSelf,
                CanGrantAdmin = canGrantAdmin,
                AllRoles = assignableRoles,
                SelectedRoles = isSelf
                    ? currentRoles
                    : new List<string> { currentRoles.FirstOrDefault(assignableRoles.Contains) ?? AppRoles.Employee }
            };
        }

        private async Task<bool> CanManageRolesAsync(IdentityUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(AppRoles.Admin) ||
                !roles.Any(r => r == AppRoles.Employee || r == AppRoles.Manager || r == AppRoles.Support))
                return false;

            var currentAdminId = _userManager.GetUserId(User);
            var hasOtherAdminGroupMembership = await _context.GroupMembers.AnyAsync(m =>
                m.UserId == user.Id &&
                !_context.Groups.Any(g => g.Id == m.GroupId && g.OwnerId == currentAdminId));
            if (hasOtherAdminGroupMembership) return false;

            var hasMembership = await _context.GroupMembers.AnyAsync(m => m.UserId == user.Id);
            if (hasMembership)
            {
                return await _context.GroupMembers.AnyAsync(m =>
                    m.UserId == user.Id &&
                    _context.Groups.Any(g => g.Id == m.GroupId && g.OwnerId == currentAdminId));
            }



            return user.EmailConfirmed;
        }

        private async Task<bool> CanGrantAdminAsync(IdentityUser user)
        {
            return user.EmailConfirmed && await CanManageRolesAsync(user) &&
                !await _context.GroupMembers.AnyAsync(m => m.UserId == user.Id);
        }

        private async Task<bool> IsManagedEmployeeAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !await _userManager.IsInRoleAsync(user, AppRoles.Employee)) return false;

            var adminId = _userManager.GetUserId(User);
            return await _context.GroupMembers.AnyAsync(m =>
                m.UserId == userId &&
                _context.Groups.Any(g => g.Id == m.GroupId && g.OwnerId == adminId));
        }
    }
}
