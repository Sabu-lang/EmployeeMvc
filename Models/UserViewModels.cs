namespace EmployeeMvc.Models
{
    public class UserListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool IsLockedOut { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool CanManageRoles { get; set; }
        public bool CanDelete { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class UserRolesViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsSelf { get; set; }
        public bool CanGrantAdmin { get; set; }
        public List<string> AllRoles { get; set; } = new();
        public List<string> SelectedRoles { get; set; } = new();
    }
}
