using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{
    public partial class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly UrlEncoder _urlEncoder;
        private readonly AppSettings _appSettings;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context,
            IEmailSender emailSender,
            UrlEncoder urlEncoder,
            IOptions<AppSettings> appSettings,
            IWebHostEnvironment environment,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
            _urlEncoder = urlEncoder;
            _appSettings = appSettings.Value;
            _environment = environment;
            _logger = logger;
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
                ModelState.AddModelError(
                    string.Empty,
                    "ეს ელფოსტა უკვე დარეგისტრირებულია."
                );

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
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description
                    );
                }

                return View(model);
            }

            // Public registration never grants administrative privileges.
            var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Employee);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            if (!await TrySendVerificationCodeAsync(model.Email))
            {
                TempData["InfoMessage"] = VerificationEmailFailureMessage;
            }

            return RedirectToAction(
                nameof(VerifyEmail),
                new { email = model.Email }
            );
        }

        [HttpGet]
        public IActionResult VerifyEmail(string email)
        {
            return View(
                new VerifyEmailViewModel
                {
                    Email = email
                }
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(
            VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var record = await _context.EmailVerificationCodes
                .Where(c =>
                    c.Email == model.Email &&
                    c.Code == model.Code)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (record == null ||
                record.ExpiresAt < DateTime.Now)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "კოდი არასწორია ან მისი ვადა გავიდა. მოითხოვეთ ახალი."
                );

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

            await _signInManager.SignInAsync(
                user,
                isPersistent: false
            );

            return RedirectToAction(
                "Index",
                "Employees"
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCode(string email)
        {
            TempData["InfoMessage"] = await TrySendVerificationCodeAsync(email)
                ? "ახალი კოდი გაიგზავნა თქვენს ელფოსტაზე."
                : VerificationEmailFailureMessage;

            return RedirectToAction(
                nameof(VerifyEmail),
                new { email }
            );
        }

        private const string VerificationEmailFailureMessage =
            "ანგარიში შეიქმნა, მაგრამ Resend-მა კოდი ვერ გააგზავნა. სატესტო რეჟიმში სხვა მისამართებზე გაგზავნას შეიძლება დადასტურებული დომენი სჭირდებოდეს. შეამოწმეთ Resend-ის Domains და Logs გვერდები; შემდეგ სცადეთ კოდის თავიდან გაგზავნა.";

        private async Task<bool> TrySendVerificationCodeAsync(string email)
        {
            try
            {
                await SendVerificationCodeAsync(email);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Verification code could not be sent to {Email} through Resend.", email);

                var unsentCodes = await _context.EmailVerificationCodes
                    .Where(c => c.Email == email)
                    .ToListAsync();
                _context.EmailVerificationCodes.RemoveRange(unsentCodes);
                await _context.SaveChangesAsync();

                return false;
            }
        }

        private async Task SendVerificationCodeAsync(string email)
        {
            var oldCodes =
                _context.EmailVerificationCodes
                    .Where(c => c.Email == email);

            _context.EmailVerificationCodes.RemoveRange(oldCodes);

            // კრიპტოგრაფიულად უსაფრთხო 6-ნიშნა კოდი
            var code =
                RandomNumberGenerator
                    .GetInt32(100000, 1000000)
                    .ToString();

            _context.EmailVerificationCodes.Add(
                new EmailVerificationCode
                {
                    Email = email,
                    Code = code,
                    ExpiresAt = DateTime.Now.AddMinutes(15)
                }
            );

            await _context.SaveChangesAsync();

            var subject = "თქვენი დადასტურების კოდი";

            var htmlMessage = $@"
                <p>გამარჯობა!</p>

                <p>თქვენი ერთჯერადი დადასტურების კოდია:</p>

                <h2 style=""letter-spacing: 4px;"">
                    {code}
                </h2>

                <p>
                    კოდი მოქმედია 15 წუთის განმავლობაში.
                </p>";

            await _emailSender.SendEmailAsync(
                email,
                subject,
                htmlMessage
            );
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(
                new LoginViewModel
                {
                    ReturnUrl = returnUrl
                }
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            const string invalidCredentials =
                "ელფოსტა ან პაროლი არასწორია.";

            var user =
                await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    invalidCredentials
                );

                return View(model);
            }

            // პაროლის შემოწმება + lockout
            var check =
                await _signInManager.CheckPasswordSignInAsync(
                    user,
                    model.Password,
                    lockoutOnFailure: true
                );

            if (check.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "ანგარიში დროებით დაბლოკილია წარუმატებელი მცდელობების გამო. სცადეთ მოგვიანებით."
                );

                return View(model);
            }

            if (!check.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    invalidCredentials
                );

                return View(model);
            }

            // ელფოსტის დადასტურება
            if (!user.EmailConfirmed)
            {
                var sent = await TrySendVerificationCodeAsync(model.Email);
                TempData["InfoMessage"] = sent
                    ? "თქვენი ანგარიში ჯერ არ არის დადასტურებული. ახალი კოდი გაიგზავნა თქვენს ელფოსტაზე."
                    : VerificationEmailFailureMessage;

                return RedirectToAction(
                    nameof(VerifyEmail),
                    new { email = model.Email }
                );
            }

            // ავტორიზაცია / 2FA
            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false
                );

            if (result.RequiresTwoFactor)
            {
                return RedirectToAction(
                    nameof(LoginWith2fa),
                    new
                    {
                        returnUrl = model.ReturnUrl,
                        rememberMe = model.RememberMe
                    }
                );
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "ანგარიში დროებით დაბლოკილია. სცადეთ მოგვიანებით."
                );

                return View(model);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    invalidCredentials
                );

                return View(model);
            }

            return RedirectToLocal(model.ReturnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(
            string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(
                "Index",
                "Employees"
            );
        }
    }
}
