using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EmployeeMvc.Models;
using EmployeeMvc.Services;

namespace EmployeeMvc.Controllers
{
    // Security გვერდი და Two-Factor Authentication (ASP.NET Core Identity-ის სტანდარტული TOTP).
    public partial class AccountController
    {
        private const string AuthenticatorIssuer = "EmployeeMvc";
        private const string RecoveryCodesKey = "RecoveryCodes";

        // ---------- Security (ცენტრალური გვერდი) ----------

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Security()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var model = new SecurityViewModel
            {
                HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null,
                Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
                RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user),
                IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user)
            };

            return View(model);
        }

        // ---------- 2FA-ის ჩართვა ----------

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EnableAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // 2FA უკვე ჩართულია → საიდუმლო გასაღებს აღარ ვაჩვენებთ
            // (ხელახლა სანახავად/შესაცვლელად საჭიროა Reset პაროლით).
            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return RedirectToAction(nameof(Security));
            }

            var model = new EnableAuthenticatorViewModel();
            await LoadSharedKeyAndQrCodeAsync(user, model);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return RedirectToAction(nameof(Security));
            }

            if (!ModelState.IsValid)
            {
                await LoadSharedKeyAndQrCodeAsync(user, model);
                return View(model);
            }

            var verificationCode = NormalizeCode(model.Code);

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!isValid)
            {
                ModelState.AddModelError(nameof(model.Code), "კოდი არასწორია. შეამოწმეთ დრო ტელეფონზე და სცადეთ თავიდან.");
                await LoadSharedKeyAndQrCodeAsync(user, model);
                return View(model);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            await _signInManager.RefreshSignInAsync(user);

            if (await _userManager.CountRecoveryCodesAsync(user) == 0)
            {
                var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                TempData[RecoveryCodesKey] = string.Join(';', codes!);
                return RedirectToAction(nameof(ShowRecoveryCodes));
            }

            TempData["SecurityMessage"] = "ორფაქტორიანი ავტორიზაცია ჩაირთო.";
            return RedirectToAction(nameof(Security));
        }

        // ---------- Recovery codes ----------

        /// <summary>
        /// Recovery კოდები ნაჩვენებია მხოლოდ გენერაციისთანავე, ერთხელ (შენახული კოდების ხელახლა ნახვა შეუძლებელია).
        /// დაკარგვის შემთხვევაში — ახლის გენერაცია პაროლით დადასტურების შემდეგ.
        /// </summary>
        [Authorize]
        [HttpGet]
        public IActionResult ShowRecoveryCodes()
        {
            if (TempData[RecoveryCodesKey] is not string joined || string.IsNullOrEmpty(joined))
            {
                return RedirectToAction(nameof(Security));
            }

            return View(joined.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateRecoveryCodes(string? password)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                TempData["SecurityError"] = "Recovery კოდების გენერაცია შესაძლებელია მხოლოდ ჩართული 2FA-ს დროს.";
                return RedirectToAction(nameof(Security));
            }

            if (!await ReauthenticateAsync(user, password))
            {
                TempData["SecurityError"] = "პაროლი არასწორია.";
                return RedirectToAction(nameof(Security));
            }

            var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData[RecoveryCodesKey] = string.Join(';', codes!);
            return RedirectToAction(nameof(ShowRecoveryCodes));
        }

        // ---------- გამორთვა / გასაღების განახლება (პაროლით ხელახალი დადასტურება) ----------

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable2fa(string? password)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!await ReauthenticateAsync(user, password))
            {
                TempData["SecurityError"] = "პაროლი არასწორია — 2FA არ გამოირთო.";
                return RedirectToAction(nameof(Security));
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!result.Succeeded)
            {
                TempData["SecurityError"] = "2FA-ის გამორთვა ვერ მოხერხდა.";
                return RedirectToAction(nameof(Security));
            }

            await _signInManager.ForgetTwoFactorClientAsync();
            await _signInManager.RefreshSignInAsync(user);

            TempData["SecurityMessage"] = "ორფაქტორიანი ავტორიზაცია გამოირთო.";
            return RedirectToAction(nameof(Security));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAuthenticator(string? password)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!await ReauthenticateAsync(user, password))
            {
                TempData["SecurityError"] = "პაროლი არასწორია — გასაღები არ განახლდა.";
                return RedirectToAction(nameof(Security));
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);
            await _signInManager.ForgetTwoFactorClientAsync();
            await _signInManager.RefreshSignInAsync(user);

            TempData["SecurityMessage"] = "გასაღები განახლდა. დაასკანირეთ ახალი QR კოდი და ხელახლა დაადასტურეთ.";
            return RedirectToAction(nameof(EnableAuthenticator));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgetBrowser()
        {
            await _signInManager.ForgetTwoFactorClientAsync();
            TempData["SecurityMessage"] = "ამ ბრაუზერის დამახსოვრება გაუქმდა.";
            return RedirectToAction(nameof(Security));
        }

        // ---------- Login 2FA ეტაპი ----------

        [HttpGet]
        public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            return View(new LoginWith2faViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWith2fa(LoginWith2faViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                NormalizeCode(model.Code), model.RememberMe, model.RememberMachine);

            if (result.Succeeded)
            {
                return RedirectToLocal(model.ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "ანგარიში დროებით დაბლოკილია წარუმატებელი მცდელობების გამო. სცადეთ მოგვიანებით.");
                return View(model);
            }

            ModelState.AddModelError(nameof(model.Code), "კოდი არასწორია.");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> LoginWithRecoveryCode(string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            return View(new LoginWithRecoveryCodeViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginWithRecoveryCode(LoginWithRecoveryCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var recoveryCode = model.RecoveryCode.Replace(" ", string.Empty);
            var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

            if (result.Succeeded)
            {
                return RedirectToLocal(model.ReturnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "ანგარიში დროებით დაბლოკილია წარუმატებელი მცდელობების გამო. სცადეთ მოგვიანებით.");
                return View(model);
            }

            ModelState.AddModelError(nameof(model.RecoveryCode), "Recovery კოდი არასწორია.");
            return View(model);
        }

        // ---------- helpers ----------

        /// <summary>
        /// პაროლის ხელახალი შემოწმება მგრძნობიარე ოპერაციებისთვის.
        /// lockoutOnFailure: true — რომ გატაცებული სესიიდან პაროლის გამოცნობა ვერ მოხერხდეს.
        /// </summary>
        private async Task<bool> ReauthenticateAsync(IdentityUser user, string? password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            return result.Succeeded;
        }

        private static string NormalizeCode(string code) =>
            code.Replace(" ", string.Empty).Replace("-", string.Empty);

        private async Task LoadSharedKeyAndQrCodeAsync(IdentityUser user, EnableAuthenticatorViewModel model)
        {
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            var email = await _userManager.GetEmailAsync(user) ?? user.UserName ?? "user";

            model.SharedKey = FormatKey(key!);
            model.QrCodeDataUri = QrCodeHelper.ToPngDataUri(GenerateQrCodeUri(email, key!));
        }

        private static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            var currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        /// <summary>otpauth:// URI — Google Authenticator, Microsoft Authenticator და Authy ყველა სტანდარტულ TOTP-ს იღებს.</summary>
        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
                _urlEncoder.Encode(AuthenticatorIssuer),
                _urlEncoder.Encode(email),
                unformattedKey);
        }
    }
}
