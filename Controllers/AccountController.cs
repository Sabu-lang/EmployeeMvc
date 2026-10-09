using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EmployeeMvc.Data;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{
    public partial class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly UrlEncoder _urlEncoder;
        private readonly AppSettings _appSettings;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IEmailSender emailSender,
            UrlEncoder urlEncoder,
            IOptions<AppSettings> appSettings,
            IWebHostEnvironment environment,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _urlEncoder = urlEncoder;
            _appSettings = appSettings.Value;
            _environment = environment;
            _logger = logger;
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
