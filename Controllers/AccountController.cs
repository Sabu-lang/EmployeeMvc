using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{

    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "ეს ელფოსტა უკვე დარეგისტრირებულია.");
                return View(model);
            }

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await SendVerificationCodeAsync(model.Email);

            return RedirectToAction(nameof(VerifyEmail), new { email = model.Email });
        }


        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            return View(new VerifyEmailViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var record = await _context.EmailVerificationCodes
                .Where(c => c.Email == model.Email && c.Code == model.Code)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (record == null || record.ExpiresAt < DateTime.Now)
            {
                ModelState.AddModelError(string.Empty, "კოდი არასწორია ან მისი ვადა გავიდა. მოითხოვეთ ახალი.");
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return NotFound();
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            _context.EmailVerificationCodes.Remove(record);
            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToAction("Index", "Employees");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCode(string email)
        {
            await SendVerificationCodeAsync(email);
            TempData["InfoMessage"] = "ახალი კოდი გაიგზავნა თქვენს ელფოსტაზე.";
            return RedirectToAction(nameof(VerifyEmail), new { email });
        }

        private async Task SendVerificationCodeAsync(string email)
        {
            var oldCodes = _context.EmailVerificationCodes.Where(c => c.Email == email);
            _context.EmailVerificationCodes.RemoveRange(oldCodes);

            var code = Random.Shared.Next(100000, 999999).ToString();

            _context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                Email = email,
                Code = code,
                ExpiresAt = DateTime.Now.AddMinutes(15)
            });

            await _context.SaveChangesAsync();

            var subject = "თქვენი დადასტურების კოდი";
            var htmlMessage = $@"
                <p>გამარჯობა!</p>
                <p>თქვენი ერთჯერადი დადასტურების კოდია:</p>
                <h2 style=""letter-spacing: 4px;"">{code}</h2>
                <p>კოდი მოქმედია 15 წუთის განმავლობაში.</p>";

            await _emailSender.SendEmailAsync(email, subject, htmlMessage);
        }


        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "ელფოსტა ან პაროლი არასწორია.");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                await SendVerificationCodeAsync(model.Email);
                TempData["InfoMessage"] = "თქვენი ანგარიში ჯერ არ არის დადასტურებული. ახალი კოდი გაიგზავნა თქვენს ელფოსტაზე.";
                return RedirectToAction(nameof(VerifyEmail), new { email = model.Email });
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "ელფოსტა ან პაროლი არასწორია.");
                return View(model);
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Employees");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
    }
}
