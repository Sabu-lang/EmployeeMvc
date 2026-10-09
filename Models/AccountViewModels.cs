using System.ComponentModel.DataAnnotations;

namespace EmployeeMvc.Models
{
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


}
