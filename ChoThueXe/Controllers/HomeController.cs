using ChoThueXe.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ChoThueXe.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
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

        [HttpGet]
        public IActionResult TestAuth()
        {
            return Content($"Authenticated: {User.Identity?.IsAuthenticated}\nName: {User.Identity?.Name}\nRole: {User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value}");
        }

        [HttpGet]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendMessage(string email, string content)
        {
            // Redirect to Customer controller which has SendMessage action
            // Or handle basic validation here
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Vui long nhap email va noi dung tin nhan.";
                return RedirectToAction("Contact");
            }

            TempData["Info"] = "Tin nhan cua ban se duoc chuyen den admin.";
            return RedirectToAction("Contact");
        }
    }
}
