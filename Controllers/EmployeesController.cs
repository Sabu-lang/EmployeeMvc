using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;
using EmployeeMvc.Services;

namespace EmployeeMvc.Controllers
{
    [Authorize]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICurrentEmployeeService _currentEmployee;
        private readonly UserManager<IdentityUser> _userManager;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxPhotoSizeBytes = 5 * 1024 * 1024; 

        public EmployeesController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ICurrentEmployeeService currentEmployee,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _environment = environment;
            _currentEmployee = currentEmployee;
            _userManager = userManager;
        }

        // Admin sees employees in owned groups; Manager / Support retain their existing broad view.
        private bool IsStaff =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Support);

        // მართვა (შექმნა/რედაქტირება/წაშლა) — Admin და Manager.
        private bool CanManageAll =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        public async Task<IActionResult> Index(string? searchString, bool? isWorking)
        {
            var employees = _context.Employees
                .ScopeToAdminGroups(_context, User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                employees = employees.Where(e =>
                    e.FullName.Contains(searchString) ||
                    e.Position.Contains(searchString));
            }

            if (isWorking.HasValue)
            {
                employees = employees.Where(e => e.IsWorking == isWorking.Value);
            }

            var me = await _currentEmployee.GetAsync(User);

            if (!IsStaff)
            {
                // server-side შეზღუდვა: Employee როლს სხვა თანამშრომლების მონაცემები არ უბრუნდება.
                var myId = me?.Id;
                employees = employees.Where(e => e.Id == myId);
            }

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = isWorking;
            ViewData["CurrentEmployeeId"] = me?.Id;

            var result = await employees.OrderBy(e => e.FullName).ToListAsync();
            return View(result);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var me = await _currentEmployee.GetAsync(User);
            var isSelf = me != null && me.Id == id;

            if (!IsStaff && !isSelf) return Forbid();

            var employee = await _context.Employees
                .ScopeToAdminGroups(_context, User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            ViewData["IsSelf"] = isSelf;
            return View(employee);
        }

        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Create()
        {
            var model = new EmployeeCreateViewModel();
            await PopulateGroupOptionsAsync(model);
            PopulateRoleOptions(model);
            if (model.GroupOptions.Count == 0)
            {
                TempData["TaskError"] = "თანამშრომლის დამატებამდე შექმენით საკუთარი ჯგუფი / გუნდი.";
                return RedirectToAction("Create", "Groups");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Create(EmployeeCreateViewModel model)
        {
            var ownerId = _userManager.GetUserId(User);
            var group = await _context.Groups.FirstOrDefaultAsync(g =>
                g.Id == model.GroupId && g.OwnerId == ownerId);
            if (group == null)
            {
                ModelState.AddModelError(nameof(model.GroupId), "აირჩიეთ თქვენი ჯგუფი / გუნდი.");
            }

            var allowedRoles = User.IsInRole(AppRoles.Admin)
                ? new[] { AppRoles.Employee, AppRoles.Manager, AppRoles.Support }
                : new[] { AppRoles.Employee };
            if (!allowedRoles.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "ამ როლის მინიჭების უფლება არ გაქვთ.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) &&
                await _userManager.FindByEmailAsync(model.Email.Trim()) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "ამ ელფოსტით ანგარიში უკვე არსებობს.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) &&
                await _context.Employees.AnyAsync(e => e.Email.ToLower() == model.Email.Trim().ToLower()))
            {
                ModelState.AddModelError(nameof(model.Email), "ამ ელფოსტით თანამშრომლის ჩანაწერი უკვე არსებობს.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateGroupOptionsAsync(model);
                PopulateRoleOptions(model);
                return View(model);
            }

            string? savedPhotoPath = null;
            if (model.PhotoFile != null)
            {
                var saveResult = await SavePhotoAsync(model.PhotoFile);
                if (!saveResult.Success)
                {
                    ModelState.AddModelError(nameof(model.PhotoFile), saveResult.ErrorMessage ?? "ფოტოს ატვირთვა ვერ მოხერხდა.");
                    await PopulateGroupOptionsAsync(model);
                    PopulateRoleOptions(model);
                    return View(model);
                }
                savedPhotoPath = saveResult.SavedPath;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var account = new IdentityUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = false,
                LockoutEnabled = true
            };

            var accountResult = await _userManager.CreateAsync(account, model.Password);
            if (!accountResult.Succeeded)
            {
                await transaction.RollbackAsync();
                DeletePhotoFile(savedPhotoPath);
                foreach (var error in accountResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await PopulateGroupOptionsAsync(model);
                PopulateRoleOptions(model);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(account, model.Role);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                DeletePhotoFile(savedPhotoPath);
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await PopulateGroupOptionsAsync(model);
                PopulateRoleOptions(model);
                return View(model);
            }

            var employee = new Employee
            {
                FullName = model.FullName.Trim(),
                Position = model.Position.Trim(),
                Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim(),
                Email = model.Email.Trim(),
                HireDate = model.HireDate,
                IsWorking = model.IsWorking,
                PhotoPath = savedPhotoPath,
                AccountId = account.Id
            };

            _context.Employees.Add(employee);
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId = group!.Id,
                UserId = account.Id,
                JoinedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                DeletePhotoFile(savedPhotoPath);
                ModelState.AddModelError(string.Empty, "თანამშრომლის ანგარიშის შექმნა ვერ მოხერხდა. გადაამოწმეთ ელფოსტა და სცადეთ თავიდან.");
                await PopulateGroupOptionsAsync(model);
                PopulateRoleOptions(model);
                return View(model);
            }

            TempData["TaskSuccess"] = $"თანამშრომელი დაემატა, შეიქმნა {model.Role} როლის ანგარიში და დაემატა თქვენს ჯგუფში.";
            return RedirectToAction(nameof(Index));
        }

        private void PopulateRoleOptions(EmployeeCreateViewModel model)
        {
            var roles = User.IsInRole(AppRoles.Admin)
                ? new[] { AppRoles.Employee, AppRoles.Support, AppRoles.Manager }
                : new[] { AppRoles.Employee };

            model.RoleOptions = roles.Select(role => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = role,
                Text = role
            }).ToList();

            if (!roles.Contains(model.Role)) model.Role = AppRoles.Employee;
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var selfEdit = false;
            if (!CanManageAll)
            {
                // Employee მხოლოდ საკუთარ ჩანაწერს ხსნის; Support-ს რედაქტირება არ შეუძლია.
                var me = await _currentEmployee.GetAsync(User);
                if (me == null || me.Id != id) return Forbid();
                selfEdit = true;
            }

            var employee = await _context.Employees
                .ScopeToAdminGroups(_context, User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            ViewData["SelfEdit"] = selfEdit;
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,FullName,Position,Department,Email,HireDate,IsWorking,PhotoFile")] Employee employee)
        {
            if (id != employee.Id) return NotFound();

            if (!CanManageAll)
            {
                var me = await _currentEmployee.GetAsync(User);
                if (me == null || me.Id != id) return Forbid();

                return await EditOwnProfileAsync(employee);
            }

            var existing = await _context.Employees
                .ScopeToAdminGroups(_context, User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                employee.PhotoPath = existing.PhotoPath;
                return View(employee);
            }

            var account = string.IsNullOrWhiteSpace(existing.AccountId)
                ? null
                : await _userManager.FindByIdAsync(existing.AccountId);
            var emailChanged = account != null &&
                !string.Equals(account.Email, employee.Email, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                var duplicate = await _userManager.FindByEmailAsync(employee.Email);
                if (duplicate != null && duplicate.Id != account!.Id)
                {
                    ModelState.AddModelError(nameof(Employee.Email), "ამ ელფოსტით ანგარიში უკვე არსებობს.");
                    employee.PhotoPath = existing.PhotoPath;
                    return View(employee);
                }
            }

            var previousPhotoPath = existing.PhotoPath;
            string? savedPhotoPath = null;
            try
            {
                if (employee.PhotoFile != null)
                {
                    var saveResult = await SavePhotoAsync(employee.PhotoFile);
                    if (!saveResult.Success)
                    {
                        ModelState.AddModelError(nameof(Employee.PhotoFile), saveResult.ErrorMessage ?? "ფოტოს ატვირთვა ვერ მოხერხდა.");
                        employee.PhotoPath = existing.PhotoPath;
                        return View(employee);
                    }

                    savedPhotoPath = saveResult.SavedPath;
                }

                existing.FullName = employee.FullName;
                existing.Position = employee.Position;
                existing.Department = employee.Department;
                existing.Email = employee.Email.Trim();
                existing.HireDate = employee.HireDate;
                existing.IsWorking = employee.IsWorking;
                if (savedPhotoPath != null) existing.PhotoPath = savedPhotoPath;

                await using var transaction = await _context.Database.BeginTransactionAsync();
                if (emailChanged && account != null)
                {
                    account.Email = existing.Email;
                    account.UserName = existing.Email;
                    account.EmailConfirmed = false;
                    var updateResult = await _userManager.UpdateAsync(account);
                    if (!updateResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        foreach (var error in updateResult.Errors)
                        {
                            ModelState.AddModelError(nameof(Employee.Email), error.Description);
                        }
                        if (savedPhotoPath != null) DeletePhotoFile(savedPhotoPath);
                        employee.PhotoPath = previousPhotoPath;
                        return View(employee);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (savedPhotoPath != null) DeletePhotoFile(previousPhotoPath);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (savedPhotoPath != null) DeletePhotoFile(savedPhotoPath);
                if (!EmployeeExists(existing.Id)) return NotFound();
                throw;
            }
            catch (DbUpdateException)
            {
                if (savedPhotoPath != null) DeletePhotoFile(savedPhotoPath);
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .ScopeToAdminGroups(_context, User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            return View(employee);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees
                .ScopeToAdminGroups(_context, User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee != null)
            {
                DeletePhotoFile(employee.PhotoPath);
                if (!string.IsNullOrWhiteSpace(employee.AccountId))
                {
                    var memberships = _context.GroupMembers.Where(m => m.UserId == employee.AccountId);
                    _context.GroupMembers.RemoveRange(memberships);
                }
                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var employee = await _context.Employees
                .ScopeToAdminGroups(_context, User)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            employee.IsWorking = !employee.IsWorking;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // საკუთარი პროფილის რედაქტირება: მხოლოდ სახელი და ფოტო. Position/Department/Email/HireDate/IsWorking
        // სერვერზე არასოდეს იცვლება, რაც არ უნდა გამოაგზავნოს კლიენტმა (overposting-ის წინააღმდეგ).
        private async Task<IActionResult> EditOwnProfileAsync(Employee posted)
        {
            foreach (var key in new[]
            {
                nameof(Employee.Position), nameof(Employee.Email), nameof(Employee.Department),
                nameof(Employee.HireDate), nameof(Employee.IsWorking)
            })
            {
                ModelState.Remove(key);
            }

            var existing = await _context.Employees.FindAsync(posted.Id);
            if (existing == null) return NotFound();

            ViewData["SelfEdit"] = true;

            if (!ModelState.IsValid)
            {
                posted.PhotoPath = existing.PhotoPath;
                return View(posted);
            }

            if (posted.PhotoFile != null)
            {
                var saveResult = await SavePhotoAsync(posted.PhotoFile);
                if (!saveResult.Success)
                {
                    ModelState.AddModelError(nameof(Employee.PhotoFile), saveResult.ErrorMessage ?? "ფოტოს ატვირთვა ვერ მოხერხდა.");
                    posted.PhotoPath = existing.PhotoPath;
                    return View(posted);
                }

                DeletePhotoFile(existing.PhotoPath);
                existing.PhotoPath = saveResult.SavedPath;
            }

            existing.FullName = posted.FullName;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }

        private async Task PopulateGroupOptionsAsync(EmployeeCreateViewModel model)
        {
            var ownerId = _userManager.GetUserId(User);
            var groups = await _context.Groups.AsNoTracking()
                .Where(g => g.OwnerId == ownerId)
                .OrderBy(g => g.Name)
                .Select(g => new { g.Id, g.Name })
                .ToListAsync();

            model.GroupOptions = groups
                .Select(g => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(g.Name, g.Id.ToString()))
                .ToList();

            if (model.GroupId == null && groups.Count == 1)
            {
                model.GroupId = groups[0].Id;
            }
        }

        private sealed class PhotoSaveResult
        {
            public bool Success { get; init; }
            public string? ErrorMessage { get; init; }
            public string? SavedPath { get; init; }
        }

        private async Task<PhotoSaveResult> SavePhotoAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return new PhotoSaveResult
                {
                    Success = false,
                    ErrorMessage = "დაშვებულია მხოლოდ .jpg, .jpeg, .png, .webp ფორმატები."
                };
            }

            if (file.Length > MaxPhotoSizeBytes)
            {
                return new PhotoSaveResult
                {
                    Success = false,
                    ErrorMessage = "ფოტოს ზომა არ უნდა აღემატებოდეს 5MB-ს."
                };
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "employees");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return new PhotoSaveResult
            {
                Success = true,
                SavedPath = $"/uploads/employees/{uniqueFileName}"
            };
        }

        private void DeletePhotoFile(string? photoPath)
        {
            if (string.IsNullOrEmpty(photoPath)) return;

            var relativePath = photoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }
}
