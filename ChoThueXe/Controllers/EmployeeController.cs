using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
using ChoThueXe.Models.Rental;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace ChoThueXe.Controllers;

[Authorize(Roles = "EMPLOYEE")]
public class EmployeeController : Controller
{
    private readonly IRentalRepository _rentalRepository;

    public EmployeeController(IRentalRepository rentalRepository)
    {
        _rentalRepository = rentalRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var customers = await _rentalRepository.GetCustomersForEmployeeAsync();
        return View(new EmployeeDashboardViewModel { Customers = customers });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft(int customerId)
    {
        if (customerId <= 0)
        {
            TempData["Error"] = "Thong tin tao hop dong khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var employeeId = User.GetUserId();
            await _rentalRepository.CreateContractDraftAsync(customerId, employeeId);
            TempData["Success"] = "Nhan vien da tao hop dong draft thanh cong.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi tao hop dong: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
