namespace EmployeeMvc.Models
{
    /// <summary>სექცია "App" კონფიგურაციაში.</summary>
    public class AppSettings
    {
        /// <summary>
        /// აპლიკაციის საჯარო მისამართი (მაგ. https://employees.example.com), მხოლოდ origin, path-ის გარეშე.
        /// გამოიყენება password-reset ბმულებისთვის, რათა ბმული არ აიგოს მოთხოვნის Host header-ზე დაყრდნობით.
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// სექცია "SeedAdmin": პირველი Admin-ის შექმნისთვის. პაროლი მხოლოდ user-secrets / environment variable-დან!
    /// </summary>
    public class SeedAdminSettings
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
