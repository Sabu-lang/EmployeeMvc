using System.ComponentModel.DataAnnotations;

namespace EmployeeMvc.Models
{
    public class EmailVerificationCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(6)]
        public string Code { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }
}
