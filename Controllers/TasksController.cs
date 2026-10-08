using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public TasksController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index()
        {
            var tasks = await _context.Tasks
                .Include(t => t.Employee)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int employeeId, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["TaskError"] = "დავალების სახელი და აღწერა სავალდებულოა.";
                return RedirectToAction("Index", "Employees");
            }

            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var task = new TaskItem
            {
                EmployeeId = employeeId,
                Title = title.Trim(),
                Description = description.Trim(),
                Status = TaskBoardStatus.ToDo,
                CreatedAt = DateTime.Now
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            var subject = $"ახალი დავალება: {task.Title}";
            var htmlMessage = $@"
                <p>გამარჯობა, {employee.FullName}!</p>
                <p>თქვენ მოგენიჭათ ახალი დავალება:</p>
                <p><strong>{System.Net.WebUtility.HtmlEncode(task.Title)}</strong></p>
                <p>{System.Net.WebUtility.HtmlEncode(task.Description)}</p>
                <p>წარმატებები!</p>";

            await _emailSender.SendEmailAsync(employee.Email, subject, htmlMessage);

            TempData["TaskSuccess"] = $"დავალება წარმატებით მიენიჭა {employee.FullName}-ს და შეტყობინება გაეგზავნა მის ელფოსტაზე.";
            return RedirectToAction("Index", "Employees");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, TaskBoardStatus newStatus)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();

            task.Status = newStatus;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task != null)
            {
                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
