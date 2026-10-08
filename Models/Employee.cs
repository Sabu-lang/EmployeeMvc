using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace EmployeeMvc.Models
{
    public class Employee
    {
        public int Id { get; set; }

        /// <summary>Identity account created for this employee, when one exists.</summary>
        public string? AccountId { get; set; }

        [Required(ErrorMessage = "სახელის შეყვანა სავალდებულოა")]
        [StringLength(100)]
        [Display(Name = "სახელი და გვარი")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "პოზიციის შეყვანა სავალდებულოა")]
        [StringLength(100)]
        [Display(Name = "პოზიცია")]
        public string Position { get; set; } = string.Empty;

        [Display(Name = "დეპარტამენტი")]
        [StringLength(100)]
        public string? Department { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "არასწორი ელ.ფოსტის ფორმატი")]
        [Display(Name = "ელ.ფოსტა")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "დასაქმების თარიღი")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Display(Name = "მუშაობს ამჟამად")]
        public bool IsWorking { get; set; } = true;

        [Display(Name = "ფოტო")]
        public string? PhotoPath { get; set; }

        [NotMapped]
        [Display(Name = "ფოტოს ატვირთვა")]
        public IFormFile? PhotoFile { get; set; }
    }
}
