using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
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

    [HttpGet]
    public async Task<IActionResult> Details(int contractId)
    {
        if (contractId <= 0)
        {
            TempData["Error"] = "Hop dong khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(contractId);
            if (contract is null)
            {
                TempData["Error"] = "Khong tim thay hop dong.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.GetUserId();
            if (contract.CustomerId != userId)
            {
                TempData["Error"] = "Ban khong co quyen xem hop dong nay.";
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
            TempData["Error"] = BuildOracleErrorMessage("preview chi phi", ex);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] = "Khong the preview chi phi luc nay. Vui long thu lai.";
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
            TempData["Error"] = BuildOracleErrorMessage("tao hop dong", ex);
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

            var verification = await _rentalRepository.GetCustomerVerificationStatusAsync(input.CustomerId);
            var hasApprovedCccd = verification.HasCccd && string.Equals(verification.CccdStatus, "APPROVED", StringComparison.OrdinalIgnoreCase);
            var hasApprovedDriverLicense = verification.HasDriverLicense && string.Equals(verification.DriverLicenseStatus, "APPROVED", StringComparison.OrdinalIgnoreCase);
            if (!hasApprovedCccd || !hasApprovedDriverLicense)
            {
                TempData["Error"] = "Khach hang can duoc duyet day du CCCD va bang lai xe truoc khi thue xe.";
                return RedirectToAction(nameof(Index));
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Thue xe thanh cong bang sp_rent_vehicle. Trigger da tu dong tinh tong tien va kiem tra lich.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thue xe", ex);
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
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thanh toan", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RentVehicleInputModel input)
    {
        if (input.VehicleId <= 0)
        {
            TempData["Error"] = "ID xe khong hop le.";
            return RedirectToAction("Index", "Customer");
        }

        ModelState.Remove(nameof(RentVehicleInputModel.CustomerId));
        ModelState.Remove(nameof(RentVehicleInputModel.EmployeeId));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin thue xe khong hop le.";
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }

        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Vui long chon ngay bat dau va ngay ket thuc.";
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }

        try
        {
            var userId = User.GetUserId();
            var isVerified = await _rentalRepository.IsUserVerifiedAsync(userId);
            if (!isVerified)
            {
                TempData["Error"] = "Ban chua duoc xac minh giay to. Hay upload CCCD/Bang lai xe.";
                return RedirectToAction("Book", new { vehicleId = input.VehicleId });
            }

            input.CustomerId = userId;
            if (input.EmployeeId <= 0)
            {
                var employees = await _rentalRepository.GetUsersByRoleAsync("EMPLOYEE");
                var assignedEmployee = employees.FirstOrDefault();
                if (assignedEmployee is null)
                {
                    TempData["Error"] = "Hien khong co nhan vien nao de tiep nhan yeu cau thue xe.";
                    return RedirectToAction("Book", new { vehicleId = input.VehicleId });
                }

                input.EmployeeId = assignedEmployee.UserId;
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Dat thue xe thanh cong.";
            return RedirectToAction("Index", "Customer");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thue xe", ex);
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q, string[]? amenities)
    {
        try
        {
            var selectedAmenities = (amenities ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return RedirectToAction("Index", "Customer", new { q, amenities });
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tim kiem xe", ex);
            return RedirectToAction("Index", "Customer");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Book(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            TempData["Error"] = "ID xe khong hop le.";
            return RedirectToAction("Index", "Customer");
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesAsync();
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == vehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Khong tim thay xe da chon.";
                return RedirectToAction("Index", "Customer");
            }

            var model = new RentVehicleInputModel { VehicleId = vehicleId };
            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lay thong tin xe", ex);
            return RedirectToAction("Index", "Customer");
        }
    }

    private static string BuildOracleErrorMessage(string operation, OracleException ex)
    {
        if (ex.Number is 904 or 942 or 6550)
        {
            return $"Khong the {operation} do he thong du lieu chua san sang.";
        }

        return $"Khong the {operation} luc nay. Vui long thu lai.";
    }
}
