using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
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
            TempData["Error"] = "Hợp đồng không hợp lệ.";
            return RedirectToAction("Contracts", "Customer");
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(contractId);
            if (contract is null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng.";
                return RedirectToAction("Contracts", "Customer");
            }

            var userId = User.GetUserId();
            if (contract.CustomerId != userId)
            {
                TempData["Error"] = "Bạn không có quyền xem hợp đồng này.";
                return RedirectToAction("Contracts", "Customer");
            }

            return View(contract);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lấy chi tiết hợp đồng", ex);
            return RedirectToAction("Contracts", "Customer");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(RentVehicleInputModel input)
    {
        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Vui lòng chọn ngày bắt đầu và ngày kết thúc.";
            return RedirectToAction(nameof(Index));
        }

        if (input.StartDate > input.EndDate)
        {
            TempData["Error"] = "Ngày bắt đầu phải <= ngày kết thúc.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesAsync();
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == input.VehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Không tìm thấy xe đã chọn.";
                return RedirectToAction(nameof(Index));
            }

            var estimate = await _rentalRepository.CalculateRentalCostAsync(vehicle.PricePerDay, input.StartDate, input.EndDate);
            TempData["Info"] = $"Chi phí dự kiến cho xe {vehicle.VehicleName}: {estimate:N0} VND";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("preview chi phí", ex);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể preview chi phí lúc này. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft(CreateDraftInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin tạo hợp đồng nhập không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.CreateContractDraftAsync(input.CustomerId, input.EmployeeId);
            TempData["Success"] = "Đã tạo hợp đồng nhập (PENDING) bằng sp_create_contract.";
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tạo hợp đồng", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rent(RentVehicleInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin thuê xe nhập không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Vui lòng chọn ngày bắt đầu và ngày kết thúc.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var isVerified = await _rentalRepository.IsUserVerifiedAsync(input.CustomerId);
            var verificationView = await _rentalRepository.GetUserVerificationFromViewAsync(input.CustomerId);
            if (!isVerified || !verificationView.IsVerified)
            {
                TempData["Error"] = "Khách hàng chưa được xác minh đầy đủ CCCD và bằng lái xe. Vui lòng cho khách hàng upload CCCD/Bằng lái và chờ Admin duyệt.";
                return RedirectToAction(nameof(Index));
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Thuê xe thành công bằng sp_rent_vehicle. Trigger đã tự động tính tổng tiền và kiểm tra lịch.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thuê xe", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaymentInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.MakePaymentAsync(input);
            TempData["Success"] = "Thanh toán thành công bằng sp_make_payment. Trigger đã cập nhật trạng thái hợp đồng nếu đủ tiền.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thanh toán", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RentVehicleInputModel input)
    {
        if (input.VehicleId <= 0)
        {
            TempData["Error"] = "ID xe không hợp lệ.";
            return RedirectToAction("Index", "Customer");
        }

        ModelState.Remove(nameof(RentVehicleInputModel.CustomerId));
        ModelState.Remove(nameof(RentVehicleInputModel.EmployeeId));

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin thuê xe không hợp lệ.";
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }

        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Vui lòng chọn ngày bắt đầu và ngày kết thúc.";
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }

        try
        {
            var blockedRanges = await _rentalRepository.GetVehicleRentalDatesAsync(input.VehicleId);
            var hasOverlap = blockedRanges.Any(range =>
                range.StartDate.Date <= input.EndDate.Date
                && range.EndDate.Date.AddDays(1) >= input.StartDate.Date);

            if (hasOverlap)
            {
                TempData["Error"] = "Xe đang bận hoặc đang trong ngày bảo dưỡng (+1) theo khoảng ngày bạn chọn.";
                return RedirectToAction("Book", new
                {
                    vehicleId = input.VehicleId,
                    checkIn = input.StartDate.Date.ToString("yyyy-MM-dd"),
                    checkOut = input.EndDate.Date.ToString("yyyy-MM-dd")
                });
            }

            var userId = User.GetUserId();
            var isVerified = await _rentalRepository.IsUserVerifiedAsync(userId);
            var verificationView = await _rentalRepository.GetUserVerificationFromViewAsync(userId);
            if (!isVerified || !verificationView.IsVerified)
            {
                TempData["Error"] = "Bạn chưa được xác minh đầy đủ CCCD và bằng lái xe. Vui lòng upload CCCD/Bằng lái và đợi cho Admin duyệt trước khi xác nhận thuê xe.";
                return RedirectToAction("Book", new { vehicleId = input.VehicleId });
            }

            input.CustomerId = userId;
            if (input.EmployeeId <= 0)
            {
                var employees = await _rentalRepository.GetUsersByRoleAsync("EMPLOYEE");
                var assignedEmployee = employees.FirstOrDefault();
                if (assignedEmployee is null)
                {
                    TempData["Error"] = "Hiện không có nhân viên nào để tiếp nhận yêu cầu thuê xe.";
                    return RedirectToAction("Book", new { vehicleId = input.VehicleId });
                }

                input.EmployeeId = assignedEmployee.UserId;
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Yêu cầu thuê xe đã được tạo. Hệ thống đang chờ nhân viên/Admin duyệt.";
            return RedirectToAction("Index", "Customer");
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thuê xe", ex);
            return RedirectToAction("Book", new { vehicleId = input.VehicleId });
        }
    }

    [HttpGet]
    public IActionResult Search(string? q, string[]? amenities)
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
            TempData["Error"] = BuildOracleErrorMessage("tìm kiếm xe", ex);
            return RedirectToAction("Index", "Customer");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Book(int vehicleId, DateTime? checkIn, DateTime? checkOut)
    {
        if (vehicleId <= 0)
        {
            TempData["Error"] = "ID xe không hợp lệ.";
            return RedirectToAction("Index", "Customer");
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesAsync();
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == vehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Không tìm thấy xe đã chọn.";
                return RedirectToAction("Index", "Customer");
            }

            var model = new RentVehicleInputModel { VehicleId = vehicleId };

            if (checkIn.HasValue && checkOut.HasValue && checkOut.Value.Date >= checkIn.Value.Date)
            {
                model.StartDate = checkIn.Value.Date;
                model.EndDate = checkOut.Value.Date;
            }

            var rentalDates = await _rentalRepository.GetVehicleRentalDatesAsync(vehicleId);
            ViewBag.VehicleName = vehicle.VehicleName;
            ViewBag.VehicleBrand = vehicle.BrandName;
            ViewBag.VehicleType = vehicle.TypeName;
            ViewBag.PricePerDay = vehicle.PricePerDay;
            ViewBag.RentalDates = rentalDates;

            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lấy thông tin xe", ex);
            return RedirectToAction("Index", "Customer");
        }
    }

    private static string BuildOracleErrorMessage(string operation, OracleException ex)
    {
        if (ex.Number is 904 or 942 or 6550)
        {
            return $"Không thể {operation} do hệ thống dữ liệu chưa sẵn sàng.";
        }

        return $"Không thể {operation} lúc này. Vui lòng thử lại.";
    }
}
