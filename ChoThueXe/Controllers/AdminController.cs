using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Globalization;

namespace ChoThueXe.Controllers;

[Authorize(Roles = "ADMIN")]
public class AdminController : Controller
{
    private readonly IRentalRepository _rentalRepository;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AdminController(IRentalRepository rentalRepository, IWebHostEnvironment webHostEnvironment)
    {
        _rentalRepository = rentalRepository;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(VehicleManagement));
    }

    [HttpGet]
    public async Task<IActionResult> VehicleManagement(string? vehicleQ)
    {
        try
        {
            var brandsTask = _rentalRepository.GetBrandsAsync();
            var typesTask = _rentalRepository.GetTypesAsync();
            var amenitiesTask = _rentalRepository.GetAmenityOptionsAsync();
            var vehiclesTask = _rentalRepository.GetVehiclesAsync();

            await Task.WhenAll(
                brandsTask,
                typesTask,
                amenitiesTask,
                vehiclesTask);

            var filteredVehicles = ApplyVehicleFilters(vehiclesTask.Result, vehicleQ);
            ViewData["VehicleKeywordFilter"] = (vehicleQ ?? string.Empty).Trim();

            return View(new AdminDashboardViewModel
            {
                Brands = brandsTask.Result,
                Types = typesTask.Result,
                AmenityOptions = amenitiesTask.Result,
                Vehicles = filteredVehicles,
            });
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể tải tab quản lý xe lúc này. Vui lòng thử lại.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> CustomerManagement(string? customerQ)
    {
        try
        {
            var docsTask = _rentalRepository.GetPendingDocumentsAsync();
            var verificationTask = _rentalRepository.GetPendingVerificationsAsync();
            var profileUpdateTask = _rentalRepository.GetPendingProfileUpdateRequestsAsync();
            var messagesTask = _rentalRepository.GetMessagesForAdminAsync();
            var accountsTask = _rentalRepository.GetAdminAccountsAsync();

            await Task.WhenAll(docsTask, verificationTask, profileUpdateTask, messagesTask, accountsTask);

            ViewData["CustomerKeywordFilter"] = (customerQ ?? string.Empty).Trim();

            return View(new AdminDashboardViewModel
            {
                PendingDocuments = ApplyPendingDocumentFilters(docsTask.Result, customerQ),
                PendingVerifications = ApplyPendingVerificationFilters(verificationTask.Result, customerQ),
                PendingProfileUpdates = ApplyPendingProfileUpdateFilters(profileUpdateTask.Result, customerQ),
                Messages = messagesTask.Result,
                Accounts = ApplyAccountFilters(accountsTask.Result, customerQ)
            });
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể tải tab quản lý khách hàng lúc này. Vui lòng thử lại.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount(AdminAccountCrudInputModel input)
    {
        input.UserId = 0;
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin tạo tài khoản không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.CreateAdminAccountAsync(input);
            TempData["Success"] = "Đã tạo tài khoản mới thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tạo tài khoản", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccount(AdminAccountCrudInputModel input)
    {
        if (input.UserId <= 0 || !ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin cập nhật tài khoản không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.UpdateAdminAccountAsync(input);
            TempData["Success"] = "Đã cập nhật tài khoản thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cập nhật tài khoản", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(int userId)
    {
        if (userId <= 0)
        {
            TempData["Error"] = "Tài khoản không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        if (userId == User.GetUserId())
        {
            TempData["Error"] = "Không thể xóa chính tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.DeleteAdminAccountAsync(userId);
            TempData["Success"] = "Đã xóa tài khoản thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("xóa tài khoản", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpGet]
    public async Task<IActionResult> RentalManagement(string? contractStatus, string? contractQ)
    {
        try
        {
            var contracts = await _rentalRepository.GetContractsAsync();
            var filteredContracts = ApplyContractFilters(contracts, contractStatus, contractQ);

            ViewData["ContractStatusFilter"] = (contractStatus ?? string.Empty).Trim();
            ViewData["ContractKeywordFilter"] = (contractQ ?? string.Empty).Trim();

            return View(new AdminDashboardViewModel
            {
                Contracts = filteredContracts
            });
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể tải tab quản lý đơn thuê lúc này. Vui lòng thử lại.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> ContractDetails(int contractId)
    {
        if (contractId <= 0)
        {
            TempData["Error"] = "Hợp đồng không hợp lệ.";
            return RedirectToAction(nameof(RentalManagement));
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(contractId);
            if (contract is null)
            {
                TempData["Error"] = "Không tìm thấy hợp đồng.";
                return RedirectToAction(nameof(RentalManagement));
            }

            return View(contract);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lấy chi tiết hợp đồng", ex);
            return RedirectToAction(nameof(RentalManagement));
        }
    }

    [HttpGet]
    public async Task<IActionResult> RevenueReport()
    {
        try
        {
            var revenueByVehicleTask = _rentalRepository.GetRevenueAsync();
            var contractsTask = _rentalRepository.GetContractsAsync();
            var revenueByAccountTask = _rentalRepository.GetRevenueByAccountAsync();

            await Task.WhenAll(revenueByVehicleTask, contractsTask, revenueByAccountTask);

            var completedContracts = contractsTask.Result
                .Where(IsCompletedContractStatus)
                .GroupBy(c => c.ContractId)
                .Select(group => group.First())
                .ToList();

            var revenueByMonth = completedContracts
                .GroupBy(c => new { c.StartDate.Year, c.StartDate.Month })
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Month)
                .Select(group => new RevenuePeriodPointViewModel
                {
                    PeriodCode = $"{group.Key.Year:D4}-{group.Key.Month:D2}",
                    PeriodLabel = $"Tháng {group.Key.Month:D2}/{group.Key.Year}",
                    TotalRevenue = group.Sum(x => x.TotalAmount),
                    ContractCount = group.Count()
                })
                .TakeLast(12)
                .ToList();

            var revenueByWeek = completedContracts
                .GroupBy(c => new
                {
                    Year = ISOWeek.GetYear(c.StartDate),
                    Week = ISOWeek.GetWeekOfYear(c.StartDate)
                })
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Week)
                .Select(group => new RevenuePeriodPointViewModel
                {
                    PeriodCode = $"{group.Key.Year:D4}-W{group.Key.Week:D2}",
                    PeriodLabel = $"Tuần {group.Key.Week:D2}/{group.Key.Year}",
                    TotalRevenue = group.Sum(x => x.TotalAmount),
                    ContractCount = group.Count()
                })
                .TakeLast(12)
                .ToList();

            var topCustomers = completedContracts
                .GroupBy(c => new { c.CustomerId, Name = string.IsNullOrWhiteSpace(c.FullName) ? c.CustomerName : c.FullName })
                .Select(group => new TopCustomerRentalViewModel
                {
                    CustomerId = group.Key.CustomerId,
                    FullName = group.Key.Name ?? string.Empty,
                    RentalCount = group.Count(),
                    TotalSpent = group.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(x => x.RentalCount)
                .ThenByDescending(x => x.TotalSpent)
                .Take(10)
                .ToList();

            var model = new AdminRevenueReportViewModel
            {
                RevenueByVehicle = revenueByVehicleTask.Result,
                RevenueByAccount = revenueByAccountTask.Result,
                RevenueByMonth = revenueByMonth,
                RevenueByWeek = revenueByWeek,
                TopCustomersByRentals = topCustomers
            };

            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tải báo cáo doanh thu", ex);
            return RedirectToAction(nameof(RentalManagement));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDocument(int documentId)
    {
        if (documentId <= 0)
        {
            TempData["Error"] = "Document id không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.ApproveDocumentAsync(documentId, User.GetUserId());
            TempData["Success"] = "Đã duyệt giấy tờ thành công.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyệt giấy tờ", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVehicle(CreateVehicleInputModel input)
    {
        if (input.OwnerId <= 0)
        {
            input.OwnerId = User.GetUserId();
            ModelState.Remove(nameof(input.OwnerId));
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin xe không hợp lệ.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        try
        {
            await _rentalRepository.AddVehicleAsync(input);
            TempData["Success"] = "Admin đã thêm xe thành công và gửi thông báo đến khách hàng.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"{BuildOracleErrorMessage("thêm xe", ex)} (Mã lỗi ORA: {ex.Number})";
        }

        return RedirectToAction(nameof(VehicleManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBrand(string brandName)
    {
        if (string.IsNullOrWhiteSpace(brandName))
        {
            TempData["Error"] = "Tên hãng xe không hợp lệ.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        try
        {
            await _rentalRepository.CreateBrandAsync(brandName);
            TempData["Success"] = "Đã tạo hãng xe mới.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tạo hãng xe", ex);
        }

        return RedirectToAction(nameof(VehicleManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVehicleType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            TempData["Error"] = "Tên loại xe không hợp lệ.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        try
        {
            await _rentalRepository.CreateVehicleTypeAsync(typeName);
            TempData["Success"] = "Đã tạo loại xe mới.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tạo loại xe", ex);
        }

        return RedirectToAction(nameof(VehicleManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAmenity(string amenityName)
    {
        if (string.IsNullOrWhiteSpace(amenityName))
        {
            TempData["Error"] = "Tên tiện nghi không hợp lệ.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        try
        {
            await _rentalRepository.CreateAmenityAsync(amenityName);
            TempData["Success"] = "Đã tạo tiện nghi mới.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tạo tiện nghi", ex);
        }

        return RedirectToAction(nameof(VehicleManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVehicle(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            TempData["Error"] = "Xe không hợp lệ.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        try
        {
            await _rentalRepository.DeleteVehicleAsync(vehicleId, User.GetUserId());
            TempData["Success"] = "Đã xóa xe thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("xóa xe", ex);
        }

        return RedirectToAction(nameof(VehicleManagement));
    }

    [HttpGet]
    public async Task<IActionResult> EditVehicle(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            TempData["Error"] = "Xe không hợp lệ.";
            return RedirectToAction(nameof(VehicleManagement));
        }

        try
        {
            var viewModel = await _rentalRepository.GetVehicleForEditAsync(vehicleId);
            if (viewModel is null)
            {
                TempData["Error"] = "Không tìm thấy xe.";
                return RedirectToAction(nameof(VehicleManagement));
            }

            ViewBag.Brands = await _rentalRepository.GetBrandsAsync();
            ViewBag.Types = await _rentalRepository.GetTypesAsync();
            ViewBag.Amenities = await _rentalRepository.GetAmenityOptionsAsync();

            return View(viewModel);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lấy thông tin xe", ex);
            return RedirectToAction(nameof(VehicleManagement));
        }
    }

    [HttpGet]
    public async Task<IActionResult> VehicleSchedule(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            return BadRequest(new { error = "Xe không hợp lệ." });
        }

        try
        {
            var rentalDates = await _rentalRepository.GetVehicleRentalDatesAsync(vehicleId);
            var ranges = rentalDates
                .Select(d => new
                {
                    startDate = d.StartDate.ToString("yyyy-MM-dd"),
                    endDate = d.EndDate.ToString("yyyy-MM-dd")
                })
                .ToList();

            return Json(new
            {
                vehicleId,
                ranges
            });
        }
        catch (OracleException ex)
        {
            return StatusCode(500, new { error = BuildOracleErrorMessage("lấy lịch thuê xe", ex) });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVehicle(CreateVehicleInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin xe không hợp lệ.";
            return RedirectToAction(nameof(EditVehicle), new { vehicleId = input.VehicleId });
        }

        try
        {
            if (input.OwnerId <= 0)
            {
                input.OwnerId = User.GetUserId();
                ModelState.Remove(nameof(input.OwnerId));
            }

            await _rentalRepository.UpdateVehicleAsync(input);
            TempData["Success"] = "Cập nhật thông tin xe thành công.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cập nhật xe", ex);
            return RedirectToAction(nameof(EditVehicle), new { vehicleId = input.VehicleId });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyMessage(AdminReplyInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nội dung phản hồi không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.ReplyMessageAsync(input.MessageId, User.GetUserId(), input.ReplyContent);
            TempData["Success"] = "Đã gửi phản hồi cho khách hàng.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("phản hồi tin nhắn", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewDocuments(ReviewDocumentsInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu duyệt giấy tờ không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.ReviewUserDocumentsAsync(input.UserId, User.GetUserId(), input.IsMatched);
            TempData["Success"] = input.IsMatched
                ? "Đã duyệt CCCD và bằng lái, thông tin khớp."
                : "Đã từ chối bộ giấy tờ do không khớp.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyệt bộ giấy tờ", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewProfileUpdate(ReviewProfileUpdateRequestInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu duyệt cập nhật thông tin không hợp lệ.";
            return RedirectToAction(nameof(CustomerManagement));
        }

        try
        {
            await _rentalRepository.ReviewProfileUpdateRequestAsync(input.RequestId, User.GetUserId(), input.IsApproved);
            TempData["Success"] = input.IsApproved
                ? "Đã duyệt yêu cầu cập nhật thông tin cá nhân."
                : "Đã từ chối yêu cầu cập nhật thông tin cá nhân.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyệt cập nhật thông tin", ex);
        }

        return RedirectToAction(nameof(CustomerManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadVehicleImages(List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new { error = "Vui lòng chọn ít nhất 1 ảnh." });
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        const long maxSizeInBytes = 8 * 1024 * 1024;

        var webRootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            return StatusCode(500, new { error = "Không tìm thấy thư mục lưu ảnh trên hệ thống." });
        }

        var uploadFolder = Path.Combine(webRootPath, "uploads", "vehicles");
        Directory.CreateDirectory(uploadFolder);

        var urls = new List<string>();

        foreach (var file in files)
        {
            if (file is null || file.Length <= 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                return BadRequest(new { error = $"File '{file.FileName}' không hợp lệ. Chỉ hỗ trợ JPG, PNG, WEBP." });
            }

            if (file.Length > maxSizeInBytes)
            {
                return BadRequest(new { error = $"File '{file.FileName}' vượt quá 8MB." });
            }

            var safeFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadFolder, safeFileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            urls.Add($"/uploads/vehicles/{safeFileName}");
        }

        if (urls.Count == 0)
        {
            return BadRequest(new { error = "Không có file hợp lệ để tải lên." });
        }

        return Json(new { urls });
    }

    private static string BuildOracleErrorMessage(string operation, OracleException ex)
    {
        if (ex.Number is 904 or 942 or 6550)
        {
            return $"Không thể {operation} do hệ thống dữ liệu chưa sẵn sàng.";
        }

        return $"Không thể {operation} lúc này. Vui lòng thử lại.";
    }

    private static bool IsCompletedContractStatus(Models.Rental.ContractFullViewModel contract)
    {
        return IsCompletedContractStatus(contract.Status);
    }

    private static bool IsCompletedContractStatus(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();
        return normalized is "COMPLETED" or "DONE" or "FINISHED" or "PAID";
    }

    private static List<Models.Rental.ContractFullViewModel> ApplyContractFilters(
        IReadOnlyList<Models.Rental.ContractFullViewModel> contracts,
        string? contractStatus,
        string? contractQ)
    {
        var selectedStatus = (contractStatus ?? string.Empty).Trim();
        var keyword = (contractQ ?? string.Empty).Trim();

        IEnumerable<Models.Rental.ContractFullViewModel> filteredContracts = contracts;

        if (!string.IsNullOrWhiteSpace(selectedStatus) && !string.Equals(selectedStatus, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            filteredContracts = filteredContracts.Where(c =>
                string.Equals(c.Status, selectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filteredContracts = filteredContracts.Where(c =>
                c.ContractId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(c.FullName) && c.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(c.VehicleName) && c.VehicleName.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        return filteredContracts.ToList();
    }

    private static List<Models.Rental.VehicleDetailViewModel> ApplyVehicleFilters(
        IReadOnlyList<Models.Rental.VehicleDetailViewModel> vehicles,
        string? vehicleQ)
    {
        var keyword = (vehicleQ ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return vehicles.ToList();
        }

        return vehicles
            .Where(v =>
                v.VehicleId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(v.VehicleName) && v.VehicleName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(v.BrandName) && v.BrandName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(v.TypeName) && v.TypeName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(v.LicensePlate) && v.LicensePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(v.Status) && v.Status.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<AdminAccountManagementViewModel> ApplyAccountFilters(
        IReadOnlyList<AdminAccountManagementViewModel> accounts,
        string? customerQ)
    {
        var keyword = (customerQ ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return accounts.ToList();
        }

        return accounts
            .Where(a =>
                a.UserId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(a.FullName) && a.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(a.Email) && a.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(a.Phone) && a.Phone.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(a.RoleName) && a.RoleName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<PendingDocumentViewModel> ApplyPendingDocumentFilters(
        IReadOnlyList<PendingDocumentViewModel> documents,
        string? customerQ)
    {
        var keyword = (customerQ ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return documents.ToList();
        }

        return documents
            .Where(d =>
                d.DocumentId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || d.UserId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(d.FullName) && d.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(d.DocType) && d.DocType.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(d.Status) && d.Status.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<PendingVerificationViewModel> ApplyPendingVerificationFilters(
        IReadOnlyList<PendingVerificationViewModel> verifications,
        string? customerQ)
    {
        var keyword = (customerQ ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return verifications.ToList();
        }

        return verifications
            .Where(v =>
                v.UserId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(v.FullName) && v.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (v.CccdDocumentId.HasValue && v.CccdDocumentId.Value.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (v.DriverLicenseDocumentId.HasValue && v.DriverLicenseDocumentId.Value.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static List<PendingProfileUpdateRequestViewModel> ApplyPendingProfileUpdateFilters(
        IReadOnlyList<PendingProfileUpdateRequestViewModel> requests,
        string? customerQ)
    {
        var keyword = (customerQ ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return requests.ToList();
        }

        return requests
            .Where(r =>
                r.RequestId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || r.UserId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(r.CurrentFullName) && r.CurrentFullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(r.RequestedFullName) && r.RequestedFullName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(r.CurrentPhone) && r.CurrentPhone.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(r.RequestedPhone) && r.RequestedPhone.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
