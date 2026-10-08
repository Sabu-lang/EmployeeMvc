using System.ComponentModel.DataAnnotations;

namespace EmployeeMvc.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "ელფოსტის შეყვანა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ელფოსტის ფორმატი")]
        [Display(Name = "ელფოსტა")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლის შეყვანა სავალდებულოა")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "პაროლი უნდა შეიცავდეს მინიმუმ 6 სიმბოლოს")]
        [DataType(DataType.Password)]
        [Display(Name = "პაროლი")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლის დადასტურება სავალდებულოა")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "პაროლები არ ემთხვევა")]
        [Display(Name = "გაიმეორეთ პაროლი")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "ელფოსტის შეყვანა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ელფოსტის ფორმატი")]
        [Display(Name = "ელფოსტა")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლის შეყვანა სავალდებულოა")]
        [DataType(DataType.Password)]
        [Display(Name = "პაროლი")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "დამახსოვრება")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class VerifyEmailViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "კოდის შეყვანა სავალდებულოა")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "კოდი უნდა შედგებოდეს 6 ციფრისგან")]
        [Display(Name = "დადასტურების კოდი")]
        public string Code { get; set; } = string.Empty;
    }
}
