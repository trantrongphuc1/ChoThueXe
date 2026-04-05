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
        try
        {
            var customers = await _rentalRepository.GetCustomersForEmployeeAsync();
            var pendingContracts = await _rentalRepository.GetPendingContractsAsync();
            var vehicles = await _rentalRepository.GetVehiclesAsync();
            return View(new EmployeeDashboardViewModel 
            { 
                Customers = customers,
                PendingContracts = pendingContracts,
                Vehicles = vehicles
            });
        }
        catch (Exception)
        {
            TempData["Error"] = "Khong the tai dashboard nhan vien luc nay. Vui long thu lai.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int? contractId, int? id)
    {
        var resolvedContractId = contractId ?? id ?? 0;

        if (resolvedContractId <= 0)
        {
            TempData["Error"] = "Hop dong khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(resolvedContractId);
            if (contract is null)
            {
                TempData["Error"] = "Khong tim thay hop dong.";
                return RedirectToAction(nameof(Index));
            }

            return View(contract);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lay chi tiet hop dong", ex);
            return RedirectToAction(nameof(Index));
        }
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
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tao hop dong", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveContract(int contractId)
    {
        if (contractId <= 0)
        {
            TempData["Error"] = "Hop dong khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ApproveContractAsync(contractId);
            TempData["Success"] = "Da duyet hop dong thanh cong. Khach hang co the thanh toan.";
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex) when (ex.Message.Contains("User chua xac minh"))
        {
            TempData["Error"] = "Khach hang chua xac minh giay to. Vui long yeu cau khach hang submit CCCD/Bang lai trc.";
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyet hop dong", ex);
        }
        catch (Exception)
        {
            TempData["Error"] = "Khong the duyet hop dong luc nay. Vui long thu lai.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string BuildOracleErrorMessage(string operation, OracleException ex)
    {
        if (ex.Number is 904 or 942 or 4043 or 6508 or 6550)
        {
            return $"Khong the {operation} do he thong du lieu chua san sang.";
        }

        if (ex.Number == 1031)
        {
            return $"Khong the {operation} do tai khoan DB chua duoc cap quyen EXECUTE procedure.";
        }

        if (ex.Number == 6553)
        {
            return $"Khong the {operation} do quyen role/procedure tren DB chua duoc cap dung.";
        }

        return $"Khong the {operation} luc nay. Vui long thu lai.";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVehicle(CreateVehicleInputModel input)
    {
        TempData["Error"] = "Nhan vien khong co quyen tao xe. Vui long lien he Admin.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Vehicles()
    {
        return RedirectToAction(nameof(Index), new { focus = "vehicles" });
    }

    [HttpGet]
    public IActionResult ManageVehicles()
    {
        return RedirectToAction(nameof(Index), new { focus = "vehicles" });
    }

    [HttpGet]
    public IActionResult Customers()
    {
        return RedirectToAction(nameof(Index), new { focus = "customers" });
    }

    [HttpGet]
    public IActionResult Bookings()
    {
        return RedirectToAction(nameof(Index), new { focus = "contracts" });
    }

    [HttpGet]
    public IActionResult Revenue()
    {
        TempData["Error"] = "Bao cao doanh thu cho nhan vien dang duoc cap nhat. Vui long quay lai sau.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Reviews()
    {
        TempData["Error"] = "Chuc nang quan ly danh gia dang duoc cap nhat.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Support()
    {
        TempData["Error"] = "Chuc nang ho tro cho nhan vien dang duoc cap nhat.";
        return RedirectToAction(nameof(Index));
    }
}
