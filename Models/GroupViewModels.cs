using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EmployeeMvc.Models
{
    public class GroupFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "ჯგუფის სახელი სავალდებულოა")]
        [StringLength(100, ErrorMessage = "სახელი მაქსიმუმ 100 სიმბოლო უნდა იყოს")]
        [Display(Name = "ჯგუფის სახელი")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "აღწერა მაქსიმუმ 500 სიმბოლო უნდა იყოს")]
        [Display(Name = "აღწერა")]
        public string? Description { get; set; }

        [Display(Name = "ჯგუფის მფლობელი (Manager)")]
        public string? OwnerId { get; set; }


        public bool CanChooseOwner { get; set; }

        [ValidateNever]
        public List<SelectListItem> OwnerOptions { get; set; } = new();
    }

    public class GroupListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public bool CanManage { get; set; }
    }

    public class GroupMemberViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class GroupDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool CanManage { get; set; }
        public List<GroupMemberViewModel> Members { get; set; } = new();
        public List<SelectListItem> Candidates { get; set; } = new();
    }
}
