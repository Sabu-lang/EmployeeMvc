using System.ComponentModel.DataAnnotations;

namespace EmployeeMvc.Models
{
    // შენიშვნა: MVC non-nullable string თვისებებს implicit [Required]-ად თვლის,
    // ამიტომ ფორმიდან არგამოგზავნილი ველები აქ string?-ია.

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "ელფოსტის შეყვანა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ელფოსტის ფორმატი")]
        [Display(Name = "ელფოსტა")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "ელფოსტის შეყვანა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ელფოსტის ფორმატი")]
        [Display(Name = "ელფოსტა")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "ახალი პაროლის შეყვანა სავალდებულოა")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "პაროლი უნდა შეიცავდეს მინიმუმ 6 სიმბოლოს")]
        [DataType(DataType.Password)]
        [Display(Name = "ახალი პაროლი")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლის დადასტურება სავალდებულოა")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "პაროლები არ ემთხვევა")]
        [Display(Name = "გაიმეორეთ პაროლი")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;
    }

    public class SecurityViewModel
    {
        public bool HasAuthenticator { get; set; }
        public bool Is2faEnabled { get; set; }
        public int RecoveryCodesLeft { get; set; }
        public bool IsMachineRemembered { get; set; }
    }

    public class EnableAuthenticatorViewModel
    {
        [Required(ErrorMessage = "კოდის შეყვანა სავალდებულოა")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "კოდი უნდა შედგებოდეს 6 ციფრისგან")]
        [Display(Name = "დადასტურების კოდი")]
        public string Code { get; set; } = string.Empty;

        public string? SharedKey { get; set; }
        public string? QrCodeDataUri { get; set; }
    }

    public class LoginWith2faViewModel
    {
        [Required(ErrorMessage = "კოდის შეყვანა სავალდებულოა")]
        [StringLength(7, MinimumLength = 6, ErrorMessage = "კოდი უნდა შედგებოდეს 6 ციფრისგან")]
        [Display(Name = "Authenticator კოდი")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "დაიმახსოვრე ეს მოწყობილობა")]
        public bool RememberMachine { get; set; }

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class LoginWithRecoveryCodeViewModel
    {
        [Required(ErrorMessage = "Recovery კოდის შეყვანა სავალდებულოა")]
        [Display(Name = "Recovery კოდი")]
        public string RecoveryCode { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
