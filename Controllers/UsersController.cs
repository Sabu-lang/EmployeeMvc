using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{
    /// <summary>მომხმარებლებისა და როლების მართვა — მხოლოდ Admin.</summary>
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

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);

            var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();

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
                Roles = roleRows.Where(r => r.UserId == u.Id && r.RoleName != null).Select(r => r.RoleName!).OrderBy(n => n).ToList()
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Roles(string? id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(await BuildRolesModelAsync(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Roles(string id, string[]? selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // მხოლოდ ცნობილი როლები (client-ის მიერ გამოგონილი როლი არ მიიღება).
            var desired = (selectedRoles ?? Array.Empty<string>())
                .Where(r => AppRoles.All.Contains(r))
                .Distinct()
                .ToList();

            if (desired.Count == 0)
            {
                TempData["UserError"] = "მომხმარებელს მინიმუმ ერთი როლი უნდა ჰქონდეს.";
                return RedirectToAction(nameof(Roles), new { id });
            }

            // Admin-ს საკუთარი Admin როლის ჩამოშორება ეკრძალება — სისტემა Admin-ის გარეშე არ უნდა დარჩეს.
            if (user.Id == _userManager.GetUserId(User) && !desired.Contains(AppRoles.Admin))
            {
                TempData["UserError"] = "საკუთარ თავს Admin როლს ვერ ჩამოაშორებთ.";
                return RedirectToAction(nameof(Roles), new { id });
            }

            var current = await _userManager.GetRolesAsync(user);

            var toAdd = desired.Except(current).ToList();
            var toRemove = current.Except(desired).ToList();

            if (toRemove.Count > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                {
                    TempData["UserError"] = string.Join(" ", removed.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Roles), new { id });
                }
            }

            if (toAdd.Count > 0)
            {
                var added = await _userManager.AddToRolesAsync(user, toAdd);
                if (!added.Succeeded)
                {
                    TempData["UserError"] = string.Join(" ", added.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Roles), new { id });
                }
            }

            // ძველი სესიების role claim-ები გაუქმდეს.
            await _userManager.UpdateSecurityStampAsync(user);

            // თუ Admin საკუთარ როლებს ცვლის, მიმდინარე სესია განვაახლოთ, რომ არ გავიდეს სისტემიდან.
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

            if (await _context.Groups.AnyAsync(g => g.OwnerId == user.Id))
            {
                TempData["UserError"] = "მომხმარებელი ჯგუფების მფლობელია. ჯერ შეცვალეთ ამ ჯგუფების მფლობელი ან წაშალეთ ისინი.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user); // GroupMembers cascade-ით იშლება
            TempData[result.Succeeded ? "UserSuccess" : "UserError"] = result.Succeeded
                ? "მომხმარებელი წაიშალა."
                : string.Join(" ", result.Errors.Select(e => e.Description));

            return RedirectToAction(nameof(Index));
        }

        private async Task<UserRolesViewModel> BuildRolesModelAsync(IdentityUser user)
        {
            return new UserRolesViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? user.Id,
                AllRoles = AppRoles.All.ToList(),
                SelectedRoles = (await _userManager.GetRolesAsync(user)).ToList()
            };
        }
    }
}
