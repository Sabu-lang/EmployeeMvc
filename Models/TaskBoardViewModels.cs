namespace EmployeeMvc.Models
{
    public class TaskBoardItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskBoardStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public List<TaskSubmissionItemViewModel> Submissions { get; set; } = new();
    }

    public class TaskSubmissionItemViewModel
    {
        public int Id { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public TaskBoardStatus? ReviewedStatus { get; set; }
    }
}
