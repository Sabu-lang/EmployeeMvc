using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;
using EmployeeMvc.Services;

namespace EmployeeMvc.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private const long MaxSubmissionSize = 5 * 1024 * 1024;
        private static readonly HashSet<string> AllowedSubmissionExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".jpg", ".jpeg", ".png", ".webp", ".zip"
        };

        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ICurrentEmployeeService _currentEmployee;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            ApplicationDbContext context,
            IEmailSender emailSender,
            ICurrentEmployeeService currentEmployee,
            ILogger<TasksController> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _currentEmployee = currentEmployee;
            _logger = logger;
        }

        private bool IsStaff =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager) || User.IsInRole(AppRoles.Support);

        private bool CanReview =>
            User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        public async Task<IActionResult> Index()
        {
            var query = _context.Tasks.AsNoTracking().AsQueryable();
            var me = await _currentEmployee.GetAsync(User);
            int? currentEmployeeId = me?.Id;

            if (IsStaff)
            {
                var visibleEmployeeIds = _context.Employees
                    .ScopeToVisibleGroups(_context, User)
                    .Select(e => e.Id);
                query = query.Where(t => visibleEmployeeIds.Contains(t.EmployeeId));
            }
            else
            {
                query = query.Where(t => t.EmployeeId == currentEmployeeId);
            }

            ViewData["CurrentEmployeeId"] = currentEmployeeId;
            ViewData["CanReview"] = CanReview;
            ViewData["CanApproveSubmission"] = User.IsInRole(AppRoles.Admin);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TaskBoardItemViewModel
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    EmployeeId = t.EmployeeId,
                    EmployeeName = t.Employee == null ? "უცნობი" : t.Employee.FullName,
                    Submissions = t.Submissions
                        .OrderBy(s => s.SubmittedAt)
                        .ThenBy(s => s.Id)
                        .Select(s => new TaskSubmissionItemViewModel
                        {
                            Id = s.Id,
                            Comment = s.Comment,
                            FileName = s.FileName,
                            SubmittedAt = s.SubmittedAt,
                            ReviewComment = s.ReviewComment,
                            ReviewedAt = s.ReviewedAt,
                            ReviewedStatus = s.ReviewedStatus
                        })
                        .ToList()
                })
                .ToListAsync();

            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public Task<IActionResult> Create(int employeeId, string title, string description) =>
            CreateForEmployees(new[] { employeeId }, title, description);

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public Task<IActionResult> CreateBulk(int[]? employeeIds, string title, string description) =>
            CreateForEmployees(employeeIds ?? Array.Empty<int>(), title, description);

        private async Task<IActionResult> CreateForEmployees(int[] employeeIds, string title, string description)
        {
            var ids = employeeIds.Distinct().ToArray();
            if (ids.Length == 0 || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["TaskError"] = "მონიშნეთ მინიმუმ ერთი თანამშრომელი და შეავსეთ დავალების სახელი და აღწერა.";
                return RedirectToAction("Index", "Employees");
            }

            if (title.Trim().Length > 150 || description.Trim().Length > 2000)
            {
                TempData["TaskError"] = "დავალების სახელი ან აღწერა დასაშვებ სიგრძეზე მეტია.";
                return RedirectToAction("Index", "Employees");
            }

            var currentEmployee = await _currentEmployee.GetAsync(User);
            if (User.IsInRole(AppRoles.Manager) && currentEmployee != null && ids.Contains(currentEmployee.Id))
            {
                TempData["TaskError"] = "მენეჯერს საკუთარი თანამშრომლის ჩანაწერისთვის დავალების მინიჭება არ შეუძლია.";
                return RedirectToAction("Index", "Employees");
            }

            var employees = await _context.Employees
                .ScopeToVisibleGroups(_context, User)
                .Where(e => ids.Contains(e.Id))
                .ToListAsync();

            if (employees.Count != ids.Length)
            {
                return Forbid();
            }

            var tasks = employees.Select(employee => new TaskItem
            {
                EmployeeId = employee.Id,
                Title = title.Trim(),
                Description = description.Trim(),
                Status = TaskBoardStatus.ToDo,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.Tasks.AddRange(tasks);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var notificationsFailed = 0;
            foreach (var employee in employees)
            {
                try
                {
                    var htmlMessage = $@"
                        <p>გამარჯობა, {System.Net.WebUtility.HtmlEncode(employee.FullName)}!</p>
                        <p>თქვენ მოგენიჭათ ახალი დავალება:</p>
                        <p><strong>{System.Net.WebUtility.HtmlEncode(title.Trim())}</strong></p>
                        <p>{System.Net.WebUtility.HtmlEncode(description.Trim())}</p>";
                    await _emailSender.SendEmailAsync(employee.Email, $"ახალი დავალება: {title.Trim()}", htmlMessage);
                }
                catch (Exception ex)
                {
                    notificationsFailed++;
                    _logger.LogWarning(ex, "Task notification could not be sent to {Email}.", employee.Email);
                }
            }

            TempData["TaskSuccess"] = notificationsFailed == 0
                ? $"დავალება მიენიჭა {employees.Count} თანამშრომელს. თითოეული მხოლოდ საკუთარ დავალებას დაინახავს."
                : $"დავალება მიენიჭა {employees.Count} თანამშრომელს, მაგრამ {notificationsFailed} ელფოსტის შეტყობინება ვერ გაიგზავნა.";
            return RedirectToAction("Index", "Employees");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int taskId, string? comment, IFormFile? attachment)
        {
            var employee = await _currentEmployee.GetAsync(User);
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (employee == null || task == null || task.EmployeeId != employee.Id)
            {
                return Forbid();
            }

            if (task.Status is TaskBoardStatus.Done or TaskBoardStatus.PendingReview)
            {
                TempData["TaskError"] = "ეს დავალება ამჟამად ჩაბარებას არ იღებს.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length > 2000 ||
                attachment == null || attachment.Length == 0)
            {
                TempData["TaskError"] = "ატვირთეთ ნამუშევრის ფაილი და დაურთეთ კომენტარი.";
                return RedirectToAction(nameof(Index));
            }

            if (attachment.Length > MaxSubmissionSize)
            {
                TempData["TaskError"] = "ფაილის ზომა 5 MB-ს არ უნდა აღემატებოდეს.";
                return RedirectToAction(nameof(Index));
            }

            var fileName = Path.GetFileName(attachment.FileName);
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 ||
                !AllowedSubmissionExtensions.Contains(extension))
            {
                TempData["TaskError"] = "ფაილის ფორმატი მხარდაჭერილი არ არის.";
                return RedirectToAction(nameof(Index));
            }

            await using var stream = new MemoryStream();
            await attachment.CopyToAsync(stream);
            var submission = new TaskSubmission
            {
                TaskItemId = task.Id,
                Comment = comment.Trim(),
                FileName = fileName,
                ContentType = "application/octet-stream",
                FileContent = stream.ToArray(),
                SubmittedAt = DateTime.UtcNow
            };

            _context.TaskSubmissions.Add(submission);
            task.Status = TaskBoardStatus.PendingReview;
            await _context.SaveChangesAsync();

            TempData["TaskSuccess"] = "ნამუშევარი გაიგზავნა შემოწმებაზე.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> ReviewSubmission(int submissionId, TaskBoardStatus decision, string? reviewComment)
        {
            if (decision is not (TaskBoardStatus.InProgress or TaskBoardStatus.Done) ||
                (reviewComment?.Length ?? 0) > 2000)
            {
                return BadRequest();
            }

            var submission = await _context.TaskSubmissions
                .Include(s => s.TaskItem)
                .FirstOrDefaultAsync(s => s.Id == submissionId);
            if (submission?.TaskItem == null) return NotFound();

            var task = submission.TaskItem;
            if (!await _context.Employees.ScopeToVisibleGroups(_context, User)
                    .AnyAsync(e => e.Id == task.EmployeeId))
            {
                return Forbid();
            }

            var newestSubmissionId = await _context.TaskSubmissions
                .Where(s => s.TaskItemId == task.Id)
                .OrderByDescending(s => s.SubmittedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            if (newestSubmissionId != submission.Id ||
                task.Status != TaskBoardStatus.PendingReview || submission.ReviewedAt != null)
            {
                TempData["TaskError"] = "ეს ჩაბარება უკვე შემოწმებულია ან ახალი ჩაბარებით ჩანაცვლდა.";
                return RedirectToAction(nameof(Index));
            }

            submission.ReviewedStatus = decision;
            submission.ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment.Trim();
            submission.ReviewedAt = DateTime.UtcNow;
            submission.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            task.Status = decision;
            await _context.SaveChangesAsync();

            TempData["TaskSuccess"] = decision == TaskBoardStatus.Done
                ? "ჩაბარება დამტკიცდა და დავალება დასრულებულად მოინიშნა."
                : "ჩაბარება შემოწმდა და დავალება მიმდინარეობის სტატუსში გადავიდა.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSubmission(int id)
        {
            var submission = await _context.TaskSubmissions
                .Include(s => s.TaskItem)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (submission?.TaskItem == null) return NotFound();

            var employee = await _currentEmployee.GetAsync(User);
            var isAssignedEmployee = employee?.Id == submission.TaskItem.EmployeeId;
            var isVisibleToStaff = IsStaff && await _context.Employees
                .ScopeToVisibleGroups(_context, User)
                .AnyAsync(e => e.Id == submission.TaskItem.EmployeeId);
            if (!isAssignedEmployee && !isVisibleToStaff) return Forbid();

            return File(submission.FileContent, "application/octet-stream", submission.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, TaskBoardStatus newStatus)
        {
            if (!CanReview || newStatus is not (TaskBoardStatus.ToDo or TaskBoardStatus.InProgress))
            {
                return Forbid();
            }

            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound();
            if (task.Status == TaskBoardStatus.PendingReview ||
                !await _context.Employees.ScopeToVisibleGroups(_context, User)
                    .AnyAsync(e => e.Id == task.EmployeeId))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            task.Status = newStatus;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.AdminOrManager)]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task != null)
            {
                if (!await _context.Employees.ScopeToVisibleGroups(_context, User)
                        .AnyAsync(e => e.Id == task.EmployeeId))
                {
                    return Forbid();
                }

                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
