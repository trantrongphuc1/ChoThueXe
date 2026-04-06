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
            TempData["Error"] = "Không thể tải dashboard nhân viên lúc này. Vui lòng thử lại.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int? contractId, int? id)
    {
        var resolvedContractId = contractId ?? id ?? 0;

        if (resolvedContractId <= 0)
        {
            TempData["Error"] = "Hợp đồng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(resolvedContractId);
            if (contract is null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng.";
                return RedirectToAction(nameof(Index));
            }

            return View(contract);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lấy chi tiết hợp đồng", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft(int customerId)
    {
        if (customerId <= 0)
        {
            TempData["Error"] = "Thông tin tạo hợp đồng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var employeeId = User.GetUserId();
            await _rentalRepository.CreateContractDraftAsync(customerId, employeeId);
            TempData["Success"] = "Nhân viên đã tạo hợp đồng draft thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tạo hợp đồng", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveContract(int contractId)
    {
        if (contractId <= 0)
        {
            TempData["Error"] = "Hợp đồng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ApproveContractAsync(contractId);
            TempData["Success"] = "Đã duyệt hợp đồng thành công. Khách hàng có thể thanh toán.";
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex) when (ex.Message.Contains("User chua xac minh"))
        {
            TempData["Error"] = "Khách hàng chưa xác minh giấy tờ. Vui lòng yêu cầu khách hàng submit CCCD/Bằng lái trước.";
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyệt hợp đồng", ex);
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể duyệt hợp đồng lúc này. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    private static string BuildOracleErrorMessage(string operation, OracleException ex)
    {
        if (ex.Number is 904 or 942 or 4043 or 6508 or 6550)
        {
            return $"Không thể {operation} do hệ thống dữ liệu chưa sẵn sàng.";
        }

        if (ex.Number == 1031)
        {
            return $"Không thể {operation} do tài khoản DB chưa được cấp quyền EXECUTE procedure.";
        }

        if (ex.Number == 6553)
        {
            return $"Không thể {operation} do quyền role/procedure trên DB chưa được cấp đúng.";
        }

        return $"Không thể {operation} lúc này. Vui lòng thử lại.";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateVehicle(CreateVehicleInputModel input)
    {
        TempData["Error"] = "Nhân viên không có quyền tạo xe. Vui lòng liên hệ Admin.";
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
        TempData["Error"] = "Báo cáo doanh thu cho nhân viên đang được cập nhật. Vui lòng quay lại sau.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Reviews()
    {
        TempData["Error"] = "Chức năng quản lý đánh giá đang được cập nhật.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Support()
    {
        TempData["Error"] = "Chức năng hỗ trợ cho nhân viên đang được cập nhật.";
        return RedirectToAction(nameof(Index));
    }
}
