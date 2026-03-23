using ChoThueXe.Data;
using ChoThueXe.Models.Rental;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace ChoThueXe.Controllers;

 [Authorize(Roles = "CUSTOMER")]
public class RentalController : Controller
{
    private readonly IRentalRepository _rentalRepository;

    public RentalController(IRentalRepository rentalRepository)
    {
        _rentalRepository = rentalRepository;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Customer");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(RentVehicleInputModel input)
    {
        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Vui long chon ngay bat dau va ngay ket thuc.";
            return RedirectToAction(nameof(Index));
        }

        if (input.StartDate > input.EndDate)
        {
            TempData["Error"] = "Ngay bat dau phai <= ngay ket thuc.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesAsync();
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == input.VehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Khong tim thay xe da chon.";
                return RedirectToAction(nameof(Index));
            }

            var estimate = await _rentalRepository.CalculateRentalCostAsync(vehicle.PricePerDay, input.StartDate, input.EndDate);
            TempData["Info"] = $"Chi phi du kien cho xe {vehicle.VehicleName}: {estimate:N0} VND";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi Oracle khi preview chi phi: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft(CreateDraftInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin tao hop dong nhap khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.CreateContractDraftAsync(input.CustomerId, input.EmployeeId);
            TempData["Success"] = "Da tao hop dong nhap (PENDING) bang sp_create_contract.";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi Oracle khi tao hop dong: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rent(RentVehicleInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin thue xe nhap khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Vui long chon ngay bat dau va ngay ket thuc.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var isVerified = await _rentalRepository.IsUserVerifiedAsync(input.CustomerId);
            if (!isVerified)
            {
                TempData["Error"] = "Khach hang chua duoc xac minh giay to (fn_is_user_verified = 0).";
                return RedirectToAction(nameof(Index));
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Thue xe thanh cong bang sp_rent_vehicle. Trigger da tu dong tinh tong tien va kiem tra lich.";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi Oracle khi thue xe: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaymentInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin thanh toan khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.MakePaymentAsync(input);
            TempData["Success"] = "Thanh toan thanh cong bang sp_make_payment. Trigger da cap nhat trang thai hop dong neu du tien.";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi Oracle khi thanh toan: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<RentalDashboardViewModel> BuildDashboardAsync()
    {
        var vehiclesTask = _rentalRepository.GetVehiclesAsync();
        var contractsTask = _rentalRepository.GetContractsAsync();
        var revenueTask = _rentalRepository.GetRevenueAsync();
        var usersTask = _rentalRepository.GetUsersAsync();
        var pendingContractsTask = _rentalRepository.GetPendingContractsAsync();

        await Task.WhenAll(vehiclesTask, contractsTask, revenueTask, usersTask, pendingContractsTask);

        return new RentalDashboardViewModel
        {
            Vehicles = vehiclesTask.Result,
            Contracts = contractsTask.Result,
            Revenue = revenueTask.Result,
            Users = usersTask.Result,
            PendingContracts = pendingContractsTask.Result
        };
    }
}
