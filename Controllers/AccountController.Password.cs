using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using EmployeeMvc.Models;

namespace EmployeeMvc.Controllers
{
    // Forgot Password / Reset Password
    public partial class AccountController
    {
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

                var resetPath = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, code });
                var link = BuildAbsoluteUrl(resetPath!);

                if (link == null)
                {
                    _logger.LogError("App:PublicBaseUrl არ არის კონფიგურირებული — password reset წერილი არ გაიგზავნა.");
                }
                else
                {
                    var subject = "პაროლის აღდგენა";
                    var htmlMessage = $@"
                        <p>გამარჯობა!</p>
                        <p>პაროლის აღსადგენად დააჭირეთ ქვემოთ მოცემულ ბმულს:</p>
                        <p><a href=""{System.Net.WebUtility.HtmlEncode(link)}"">პაროლის აღდგენა</a></p>
                        <p>ბმული მოქმედია 1 საათის განმავლობაში. თუ ეს თქვენ არ მოგითხოვიათ, უბრალოდ უგულებელყავით ეს წერილი.</p>";

                    await _emailSender.SendEmailAsync(user.Email!, subject, htmlMessage);
                }
            }

            // ყოველთვის ერთი და იგივე პასუხი — არ ვამჟღავნებთ, არსებობს თუ არა ანგარიში.
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string? code = null, string? email = null)
        {
            if (string.IsNullOrEmpty(code) || !TryDecodeToken(code, out _))
            {
                TempData["InfoMessage"] = "ბმული არასწორია ან ვადა გაუვიდა. მოითხოვეთ ახალი.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel { Code = code, Email = email ?? string.Empty });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!TryDecodeToken(model.Code, out var token))
            {
                return InvalidResetLink();
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // არ ვამჟღავნებთ, რომ ასეთი მომხმარებელი არ არსებობს.
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            // ResetPasswordAsync ამოწმებს ტოკენს (ვადა + ერთჯერადობა security stamp-ით) და პაროლის პოლიტიკას.
            // წარმატებისას security stamp იცვლება → ძველი პაროლი და ძველი სესიები აღარ მუშაობს.
            var result = await _userManager.ResetPasswordAsync(user, token, model.Password);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                return InvalidResetLink();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        private IActionResult InvalidResetLink()
        {
            TempData["InfoMessage"] = "ბმული არასწორია ან ვადა გაუვიდა. მოითხოვეთ ახალი.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        private static bool TryDecodeToken(string code, out string token)
        {
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
                return !string.IsNullOrEmpty(token);
            }
            catch (FormatException)
            {
                token = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// ბმულს ვაგებთ კონფიგურაციაში მითითებული საჯარო მისამართით (App:PublicBaseUrl), რათა თავდამსხმელმა
        /// Host header-ის გაყალბებით reset ბმული თავის დომენზე ვერ გადაიყვანოს. მხოლოდ Development-ში
        /// ვიყენებთ მოთხოვნის მისამართს, თუ PublicBaseUrl მითითებული არ არის.
        /// </summary>
        private string? BuildAbsoluteUrl(string relativePath)
        {
            var baseUrl = _appSettings.PublicBaseUrl;

            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                return new Uri(baseUri, relativePath).ToString();
            }

            if (_environment.IsDevelopment())
            {
                return $"{Request.Scheme}://{Request.Host}{relativePath}";
            }

            return null;
        }
    }
}
