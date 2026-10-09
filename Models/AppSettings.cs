namespace EmployeeMvc.Models
{

    public class AppSettings
    {




        public string PublicBaseUrl { get; set; } = string.Empty;
    }




    public class SeedAdminSettings
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
