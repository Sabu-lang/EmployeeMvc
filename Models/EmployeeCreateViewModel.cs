using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmployeeMvc.Models
{
    public class EmployeeCreateViewModel
    {
        [Required(ErrorMessage = "სახელის შეყვანა სავალდებულოა")]
        [StringLength(100)]
        [Display(Name = "სახელი და გვარი")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "პოზიციის შეყვანა სავალდებულოა")]
        [StringLength(100)]
        [Display(Name = "პოზიცია")]
        public string Position { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "დეპარტამენტი")]
        public string? Department { get; set; }

        [Required(ErrorMessage = "ელ.ფოსტის შეყვანა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ელ.ფოსტის ფორმატი")]
        [Display(Name = "ელ.ფოსტა")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "დასაქმების თარიღი")]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Display(Name = "მუშაობს ამჟამად")]
        public bool IsWorking { get; set; } = true;

        [Display(Name = "ფოტოს ატვირთვა")]
        public IFormFile? PhotoFile { get; set; }

        [Required(ErrorMessage = "პაროლის შეყვანა სავალდებულოა")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "პაროლი უნდა შეიცავდეს მინიმუმ 6 სიმბოლოს")]
        [DataType(DataType.Password)]
        [Display(Name = "დროებითი პაროლი")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლის დადასტურება სავალდებულოა")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "პაროლები არ ემთხვევა")]
        [Display(Name = "გაიმეორეთ პაროლი")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "აირჩიეთ ჯგუფი")]
        [Display(Name = "ჯგუფი / გუნდი")]
        public int? GroupId { get; set; }

        [Required(ErrorMessage = "აირჩიეთ როლი")]
        [Display(Name = "ანგარიშის როლი")]
        public string Role { get; set; } = AppRoles.Employee;

        [ValidateNever]
        public List<SelectListItem> GroupOptions { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> RoleOptions { get; set; } = new();
    }
}
