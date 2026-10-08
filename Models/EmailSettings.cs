namespace EmployeeMvc.Models
{
    /// <summary>Resend email API configuration. Keep the API key in User Secrets or an environment variable.</summary>
    public class EmailSettings
    {
        public string ResendApiKey { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = "Employee Management System";
    }
}
