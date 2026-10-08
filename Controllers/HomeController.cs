using Microsoft.AspNetCore.Mvc;

namespace EmployeeMvc.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Employees");
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
