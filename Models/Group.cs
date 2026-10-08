using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace EmployeeMvc.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "ჯგუფის სახელი სავალდებულოა")]
        [StringLength(100)]
        [Display(Name = "ჯგუფის სახელი")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "აღწერა")]
        public string? Description { get; set; }

        /// <summary>ჯგუფის მფლობელი (Manager ან Admin) — FK AspNetUsers-ზე.</summary>
        [Required]
        public string OwnerId { get; set; } = string.Empty;

        public IdentityUser? Owner { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
}
