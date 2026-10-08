using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Authorization;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{
    [Authorize]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IAuthorizationService _authorization;

        public GroupsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IAuthorizationService authorization)
        {
            _context = context;
            _userManager = userManager;
            _authorization = authorization;
        }

        private bool IsAdmin => User.IsInRole(AppRoles.Admin);

        // ---------- სია ----------
        // Admin: ყველა ჯგუფი. დანარჩენი: მხოლოდ ის ჯგუფები, სადაც მფლობელია ან წევრი.
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var query = _context.Groups.AsNoTracking().AsQueryable();
            if (!IsAdmin)
            {
                query = query.Where(g => g.OwnerId == userId || g.Members.Any(m => m.UserId == userId));
            }

            var rows = await query
                .OrderBy(g => g.Name)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    g.OwnerId,
                    OwnerEmail = g.Owner!.Email,
                    MemberCount = g.Members.Count
                })
                .ToListAsync();

            var isManager = User.IsInRole(AppRoles.Manager);
            var items = rows.Select(r => new GroupListItemViewModel
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                OwnerId = r.OwnerId,
                OwnerEmail = r.OwnerEmail ?? string.Empty,
                MemberCount = r.MemberCount,
                CanManage = IsAdmin || (isManager && r.OwnerId == userId)
            }).ToList();

            return View(items);
        }

        // ---------- დეტალები + წევრები ----------
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var group = await LoadGroupAsync(id.Value);
            if (group == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.View)).Succeeded)
            {
                return Forbid();
            }

            var canManage = (await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded;

            var memberIds = group.Members.Select(m => m.UserId).ToList();
            var roleRows = await (from ur in _context.UserRoles
                                  join r in _context.Roles on ur.RoleId equals r.Id
                                  where memberIds.Contains(ur.UserId)
                                  select new { ur.UserId, RoleName = r.Name })
                .ToListAsync();

            var vm = new GroupDetailsViewModel
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                OwnerEmail = group.Owner?.Email ?? string.Empty,
                CreatedAt = group.CreatedAt,
                CanManage = canManage,
                Members = group.Members
                    .OrderBy(m => m.User!.Email)
                    .Select(m => new GroupMemberViewModel
                    {
                        UserId = m.UserId,
                        Email = m.User?.Email ?? string.Empty,
                        JoinedAt = m.JoinedAt,
                        Roles = string.Join(", ", roleRows.Where(r => r.UserId == m.UserId).Select(r => r.RoleName))
                    })
                    .ToList()
            };

            if (canManage)
            {
                var candidates = await _userManager.Users
                    .Where(u => u.Id != group.OwnerId && !memberIds.Contains(u.Id))
                    .OrderBy(u => u.Email)
                    .Select(u => new { u.Id, u.Email })
                    .ToListAsync();

                vm.Candidates = candidates
                    .Select(u => new SelectListItem(u.Email ?? u.Id, u.Id))
                    .ToList();
            }

            return View(vm);
        }

        // ---------- შექმნა (Admin, Manager) ----------
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Create()
        {
            var vm = new GroupFormViewModel
            {
                CanChooseOwner = IsAdmin,
                OwnerId = _userManager.GetUserId(User)
            };
            if (IsAdmin) vm.OwnerOptions = await GetOwnerOptionsAsync();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Create(GroupFormViewModel model)
        {
            model.CanChooseOwner = IsAdmin;

            // Manager-ი ყოველთვის თვითონ ხდება მფლობელი — OwnerId ფორმიდან მისთვის იგნორირდება.
            var ownerId = IsAdmin ? model.OwnerId : _userManager.GetUserId(User);

            if (!ModelState.IsValid || !await IsValidOwnerAsync(ownerId))
            {
                if (ModelState.IsValid) ModelState.AddModelError(nameof(model.OwnerId), "მფლობელი უნდა იყოს Manager ან Admin.");
                if (IsAdmin) model.OwnerOptions = await GetOwnerOptionsAsync();
                return View(model);
            }

            var group = new Group
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                OwnerId = ownerId!,
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            TempData["GroupSuccess"] = "ჯგუფი შეიქმნა.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        // ---------- რედაქტირება (Admin ან ჯგუფის მფლობელი Manager) ----------
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var group = await LoadGroupAsync(id.Value);
            if (group == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded)
            {
                return Forbid();
            }

            var vm = new GroupFormViewModel
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                OwnerId = group.OwnerId,
                CanChooseOwner = IsAdmin
            };
            if (IsAdmin) vm.OwnerOptions = await GetOwnerOptionsAsync();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, GroupFormViewModel model)
        {
            if (model.Id != id) return NotFound();

            var group = await LoadGroupAsync(id);
            if (group == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded)
            {
                return Forbid();
            }

            model.CanChooseOwner = IsAdmin;

            // მფლობელის შეცვლა მხოლოდ Admin-ს შეუძლია.
            var newOwnerId = IsAdmin && !string.IsNullOrEmpty(model.OwnerId) ? model.OwnerId! : group.OwnerId;
            var ownerChanged = newOwnerId != group.OwnerId;

            if (!ModelState.IsValid || (ownerChanged && !await IsValidOwnerAsync(newOwnerId)))
            {
                if (ModelState.IsValid) ModelState.AddModelError(nameof(model.OwnerId), "მფლობელი უნდა იყოს Manager ან Admin.");
                if (IsAdmin) model.OwnerOptions = await GetOwnerOptionsAsync();
                return View(model);
            }

            group.Name = model.Name.Trim();
            group.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            if (ownerChanged)
            {
                group.OwnerId = newOwnerId;

                // ახალი მფლობელი წევრთა სიაში აღარ უნდა დუბლირდებოდეს.
                var duplicate = group.Members.FirstOrDefault(m => m.UserId == newOwnerId);
                if (duplicate != null) _context.GroupMembers.Remove(duplicate);
            }

            await _context.SaveChangesAsync();

            TempData["GroupSuccess"] = "ჯგუფი განახლდა.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- წაშლა ----------
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var group = await LoadGroupAsync(id.Value);
            if (group == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded)
            {
                return Forbid();
            }

            return View(group);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var group = await LoadGroupAsync(id);
            if (group == null) return RedirectToAction(nameof(Index));

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded)
            {
                return Forbid();
            }

            _context.Groups.Remove(group); // GroupMembers cascade-ით იშლება
            await _context.SaveChangesAsync();

            TempData["GroupSuccess"] = "ჯგუფი წაიშალა.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- წევრის დამატება / წაშლა ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(int groupId, string? userId)
        {
            var group = await LoadGroupAsync(groupId);
            if (group == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded)
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(userId) || await _userManager.FindByIdAsync(userId) == null)
            {
                TempData["GroupError"] = "აირჩიეთ არსებული მომხმარებელი.";
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            if (userId == group.OwnerId)
            {
                TempData["GroupError"] = "ჯგუფის მფლობელი უკვე ჯგუფის ნაწილია.";
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            if (group.Members.Any(m => m.UserId == userId))
            {
                TempData["GroupError"] = "ეს მომხმარებელი უკვე ჯგუფშია.";
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            _context.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId, JoinedAt = DateTime.UtcNow });

            try
            {
                await _context.SaveChangesAsync();
                TempData["GroupSuccess"] = "წევრი დაემატა.";
            }
            catch (DbUpdateException)
            {
                // პარალელური მოთხოვნა: composite PK (GroupId, UserId) დუბლიკატს ბაზის დონეზე ბლოკავს.
                TempData["GroupError"] = "ეს მომხმარებელი უკვე ჯგუფშია.";
            }

            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int groupId, string? userId)
        {
            var group = await LoadGroupAsync(groupId);
            if (group == null) return NotFound();

            if (!(await _authorization.AuthorizeAsync(User, group, GroupOperations.Manage)).Succeeded)
            {
                return Forbid();
            }

            var membership = group.Members.FirstOrDefault(m => m.UserId == userId);
            if (membership == null)
            {
                TempData["GroupError"] = "წევრი ვერ მოიძებნა.";
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            _context.GroupMembers.Remove(membership);
            await _context.SaveChangesAsync();

            TempData["GroupSuccess"] = "წევრი ამოიშალა ჯგუფიდან.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        // ---------- helpers ----------

        private Task<Group?> LoadGroupAsync(int id) =>
            _context.Groups
                .Include(g => g.Owner)
                .Include(g => g.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id);

        private async Task<bool> IsValidOwnerAsync(string? ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) return false;

            var owner = await _userManager.FindByIdAsync(ownerId);
            if (owner == null) return false;

            return await _userManager.IsInRoleAsync(owner, AppRoles.Manager)
                || await _userManager.IsInRoleAsync(owner, AppRoles.Admin);
        }

        private async Task<List<SelectListItem>> GetOwnerOptionsAsync()
        {
            var managers = await _userManager.GetUsersInRoleAsync(AppRoles.Manager);
            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

            return managers.Concat(admins)
                .DistinctBy(u => u.Id)
                .OrderBy(u => u.Email)
                .Select(u => new SelectListItem(u.Email ?? u.UserName ?? u.Id, u.Id))
                .ToList();
        }
    }
}
