using Microsoft.AspNetCore.Identity;

namespace EmployeeMvc.Models
{
    /// <summary>
    /// User ↔ Group many-to-many join entity.
    /// Composite PK (GroupId, UserId) ბაზის დონეზე გამორიცხავს ერთი და იგივე მომხმარებლის ორჯერ დამატებას.
    /// </summary>
    public class GroupMember
    {
        public int GroupId { get; set; }
        public Group? Group { get; set; }

        public string UserId { get; set; } = string.Empty;
        public IdentityUser? User { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
