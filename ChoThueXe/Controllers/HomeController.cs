using ChoThueXe.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ChoThueXe.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (User.IsInRole("ADMIN"))
            {
                return RedirectToAction("Index", "Admin");
            }

            if (User.IsInRole("EMPLOYEE"))
            {
                return RedirectToAction("Index", "Employee");
            }

            return RedirectToAction("Index", "Customer");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
