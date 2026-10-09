using System.ComponentModel.DataAnnotations;

namespace EmployeeMvc.Models
{
    public class TaskSubmission
    {
        public int Id { get; set; }

        [Required]
        public int TaskItemId { get; set; }
        public TaskItem? TaskItem { get; set; }

        [Required(ErrorMessage = "კომენტარი სავალდებულოა")]
        [StringLength(2000)]
        public string Comment { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string ContentType { get; set; } = "application/octet-stream";

        [Required]
        public byte[] FileContent { get; set; } = Array.Empty<byte>();

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        [StringLength(2000)]
        public string? ReviewComment { get; set; }

        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByUserId { get; set; }
        public TaskBoardStatus? ReviewedStatus { get; set; }
    }
}
