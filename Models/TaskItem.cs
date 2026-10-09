using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmployeeMvc.Models
{
    public enum TaskBoardStatus
    {
        ToDo = 0,       
        InProgress = 1, 
        Done = 2,
        PendingReview = 3
    }

    public class TaskItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "დავალების სახელი სავალდებულოა")]
        [StringLength(150)]
        [Display(Name = "დავალების სახელი")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "აღწერა სავალდებულოა")]
        [StringLength(2000)]
        [Display(Name = "აღწერა")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "სტატუსი")]
        public TaskBoardStatus Status { get; set; } = TaskBoardStatus.ToDo;

        [Display(Name = "შექმნის თარიღი")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        public ICollection<TaskSubmission> Submissions { get; set; } = new List<TaskSubmission>();
    }
}
