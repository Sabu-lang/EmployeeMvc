using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxPhotoSizeBytes = 5 * 1024 * 1024; 

        public EmployeesController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ICurrentEmployeeService currentEmployee)
        {
            _context = context;
            _environment = environment;
            _currentEmployee = currentEmployee;
        }

        // Admin / Manager / Support ხედავენ ყველა თანამშრომელს. დანარჩენს (Employee) — მხოლოდ საკუთარ ჩანაწერს.
        private bool IsStaff =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Support);

        // მართვა (შექმნა/რედაქტირება/წაშლა) — Admin და Manager.
        private bool CanManageAll =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        public async Task<IActionResult> Index(string? searchString, bool? isWorking)
        {
            var employees = _context.Employees.AsQueryable();

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

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            ViewData["IsSelf"] = isSelf;
            return View(employee);
        }

        [Authorize(Roles = AppRoles.AdminOrManager)]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Create(
            [Bind("FullName,Position,Department,Email,HireDate,IsWorking,PhotoFile")] Employee employee)
        {
            if (!ModelState.IsValid)
            {
                return View(employee);
            }

            if (employee.PhotoFile != null)
            {
                var saveResult = await SavePhotoAsync(employee.PhotoFile);
                if (!saveResult.Success)
                {
                    ModelState.AddModelError(nameof(Employee.PhotoFile), saveResult.ErrorMessage ?? "ფოტოს ატვირთვა ვერ მოხერხდა.");
                    return View(employee);
                }
                employee.PhotoPath = saveResult.SavedPath;
            }

            _context.Add(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            ViewData["SelfEdit"] = selfEdit;
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,FullName,Position,Department,Email,HireDate,IsWorking,PhotoFile,PhotoPath")] Employee employee)
        {
            if (id != employee.Id) return NotFound();

            if (!CanManageAll)
            {
                var me = await _currentEmployee.GetAsync(User);
                if (me == null || me.Id != id) return Forbid();

                return await EditOwnProfileAsync(employee);
            }

            if (!ModelState.IsValid)
            {
                return View(employee);
            }

            try
            {
                if (employee.PhotoFile != null)
                {
                    var saveResult = await SavePhotoAsync(employee.PhotoFile);
                    if (!saveResult.Success)
                    {
                        ModelState.AddModelError(nameof(Employee.PhotoFile), saveResult.ErrorMessage ?? "ფოტოს ატვირთვა ვერ მოხერხდა.");
                        return View(employee);
                    }

                    DeletePhotoFile(employee.PhotoPath);
                    employee.PhotoPath = saveResult.SavedPath;
                }

                _context.Update(employee);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.Id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();

            return View(employee);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                DeletePhotoFile(employee.PhotoPath);
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
            var employee = await _context.Employees.FindAsync(id);
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
