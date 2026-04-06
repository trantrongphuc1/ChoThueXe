using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
using ChoThueXe.Models.Rental;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace ChoThueXe.Controllers;

[Authorize(Roles = "CUSTOMER,EMPLOYEE,ADMIN")]
public class CustomerController : Controller
{
    private readonly IRentalRepository _rentalRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public CustomerController(IRentalRepository rentalRepository, IAuthRepository authRepository, IWebHostEnvironment webHostEnvironment)
    {
        _rentalRepository = rentalRepository;
        _authRepository = authRepository;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string[]? amenities, int? brandId, int? typeId, DateTime? checkIn, DateTime? checkOut)
    {
        try
        {
            var userId = User.GetUserId();
            var selectedAmenities = (amenities ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var normalizedBrandId = brandId.HasValue && brandId.Value > 0 ? brandId : null;
            var normalizedTypeId = typeId.HasValue && typeId.Value > 0 ? typeId : null;
            var normalizedCheckIn = checkIn?.Date;
            var normalizedCheckOut = checkOut?.Date;

            if (normalizedCheckIn.HasValue ^ normalizedCheckOut.HasValue)
            {
                TempData["Error"] = "Vui lòng chọn đầy đủ ngày nhận và ngày trả.";
                normalizedCheckIn = null;
                normalizedCheckOut = null;
            }
            else if (normalizedCheckIn.HasValue && normalizedCheckOut.HasValue && normalizedCheckOut.Value < normalizedCheckIn.Value)
            {
                TempData["Error"] = "Ngày trả phải lớn hơn hoặc bằng ngày nhận.";
            }

            var model = await BuildDashboardAsync(
                userId,
                q,
                selectedAmenities,
                normalizedBrandId,
                normalizedTypeId,
                normalizedCheckIn,
                normalizedCheckOut);
            return View(model);
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // Missing table - return empty dashboard
            var userId = User.GetUserId();
            var model = new CustomerDashboardViewModel
            {
                UserId = userId,
                FullName = string.Empty,
                Email = string.Empty,
                Phone = string.Empty,
                Vehicles = [],
                FavoriteVehicles = [],
                Notifications = [],
                Messages = [],
                Contracts = [],
                PendingContracts = [],
                ReviewableContracts = [],
                BrandOptions = [],
                TypeOptions = [],
                AmenityOptions = [],
                SelectedBrandId = null,
                SelectedTypeId = null,
                CheckInDate = null,
                CheckOutDate = null,
                VerificationStatus = new()
            };
            return View(model);
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể tải dashboard lúc này. Vui lòng thử lại.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Contracts()
    {
        try
        {
            var userId = User.GetUserId();
            IReadOnlyList<ContractFullViewModel> contracts = [];
            IReadOnlyList<PendingContractViewModel> pendingContracts = [];

            try
            {
                contracts = await _rentalRepository.GetContractsByCustomerAsync(userId);
            }
            catch
            {
                // Keep page alive even if this query fails in partially migrated schemas.
            }

            try
            {
                pendingContracts = await _rentalRepository.GetPendingContractsByCustomerAsync(userId);
            }
            catch
            {
                // Keep page alive even if this query fails in partially migrated schemas.
            }

            if (contracts.Count == 0 && pendingContracts.Count > 0)
            {
                contracts = pendingContracts
                    .Select(x => new ContractFullViewModel
                    {
                        ContractId = x.ContractId,
                        FullName = x.CustomerName,
                        VehicleName = "N/A",
                        StartDate = DateTime.Today,
                        EndDate = DateTime.Today,
                        TotalAmount = x.TotalAmount,
                        PaidAmount = x.PaidAmount,
                        Status = x.Status
                    })
                    .ToList();
            }

            return View(new CustomerDashboardViewModel
            {
                UserId = userId,
                Contracts = contracts,
                PendingContracts = pendingContracts
            });
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tải danh sách hợp đồng", ex);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] = "Không thể tải danh sách hợp đồng lúc này. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        try
        {
            var userId = User.GetUserId();
            var profile = await _rentalRepository.GetUserProfileAsync(userId);
            var verificationStatus = await _rentalRepository.GetCustomerVerificationStatusAsync(userId);

            var model = new UpdateProfileInputModel
            {
                FullName = profile.FullName,
                Phone = profile.Phone
            };

            ViewBag.VerificationStatus = verificationStatus;

            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tải hồ sơ", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocumentFile(IFormFile file, string? category)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest(new { error = "Vui lòng chọn file để tải lên." });
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Định dạng file không hợp lệ. Chỉ hỗ trợ JPG, PNG, PDF." });
        }

        const long maxSizeInBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeInBytes)
        {
            return BadRequest(new { error = "File vượt quá 5MB. Vui lòng chọn file nhỏ hơn." });
        }

        var webRootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            return StatusCode(500, new { error = "Không tìm thấy thư mục lưu file trên hệ thống." });
        }

        var uploadFolder = Path.Combine(webRootPath, "uploads", "documents");
        Directory.CreateDirectory(uploadFolder);

        var normalizedCategory = string.IsNullOrWhiteSpace(category)
            ? "document"
            : category.Trim().ToLowerInvariant().Replace(" ", "-").Replace("_", "-");

        if (normalizedCategory is not ("document" or "driver-license"))
        {
            normalizedCategory = "document";
        }

        var userId = User.GetUserId();
        var safeFileName = $"u{userId}_{normalizedCategory}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadFolder, safeFileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/documents/{safeFileName}";
        return Json(new { fileUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin cập nhật không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.SubmitProfileUpdateRequestAsync(User.GetUserId(), input.FullName, input.Phone);
            TempData["Success"] = "Đã gửi yêu cầu sửa thông tin. Admin sẽ duyệt trước khi cập nhật.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cập nhật thông tin", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateProfileInputModel input)
    {
        // Alias for UpdateProfile to match View form call
        return await UpdateProfile(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
        {
            TempData["Error"] = "Mật khẩu nhập lại không khớp hoặc dữ liệu không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _authRepository.ChangePasswordAsync(User.GetUserId(), currentPassword, newPassword);
            TempData["Success"] = "Đã thay đổi mật khẩu thành công.";
        }
        catch (UnauthorizedAccessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("đổi mật khẩu", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDocument(SubmitDocumentInputModel input)
    {
        if (string.IsNullOrWhiteSpace(input.DocType))
        {
            TempData["Error"] = "Vui lòng chọn loại giấy tờ.";
            return RedirectToAction(nameof(Profile));
        }

        if (string.IsNullOrWhiteSpace(input.FileUrl))
        {
            TempData["Error"] = "Vui lòng tải file lên server trước khi gửi giấy tờ.";
            return RedirectToAction(nameof(Profile));
        }

        try
        {
            await _rentalRepository.SubmitUserDocumentAsync(User.GetUserId(), input);
            TempData["Success"] = "Đã gửi giấy tờ, vui lòng đợi Admin duyệt.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gửi giấy tờ", ex);
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDriveLicense(SubmitDriveLicenseInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin bằng lái không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = User.GetUserId();
            await _rentalRepository.SubmitDriveLicenseAsync(userId, input.LicenseNumber, input.IssuedAt, input.ExpireAt, input.IssuedBy);

            if (!string.IsNullOrWhiteSpace(input.FileUrl))
            {
                await _rentalRepository.SubmitUserDocumentAsync(userId, new SubmitDocumentInputModel
                {
                    DocType = "DRIVER_LICENSES",
                    FileUrl = input.FileUrl
                });

                TempData["Success"] = "Đã gửi bằng lái (thông tin + file), vui lòng đợi Admin duyệt.";
            }
            else
            {
                TempData["Success"] = "Đã gửi thông tin bằng lái. Hãy tải thêm file bằng lái để Admin duyệt nhanh hơn.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gửi bằng lái", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(RentVehicleInputModel input)
    {
        if (input.StartDate == default || input.EndDate == default || input.StartDate > input.EndDate)
        {
            TempData["Error"] = "Ngày thuê không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesForCustomerAsync(User.GetUserId());
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == input.VehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Không tìm thấy xe đã chọn.";
                return RedirectToAction(nameof(Index));
            }

            var estimate = await _rentalRepository.CalculateRentalCostAsync(vehicle.PricePerDay, input.StartDate, input.EndDate);
            TempData["Info"] = $"Chi phí dự kiến: {estimate:N0} VND";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("preview chi phí", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rent(RentVehicleInputModel input)
    {
        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Ngày thuê không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = User.GetUserId();
            var isVerified = await _rentalRepository.IsUserVerifiedAsync(userId);
            var verificationView = await _rentalRepository.GetUserVerificationFromViewAsync(userId);
            if (!isVerified || !verificationView.IsVerified)
            {
                TempData["Error"] = "Tài khoản chưa được xác minh đầy đủ CCCD và bằng lái xe. Vui lòng cập nhật trong trang hồ sơ và cho Admin duyệt trước khi thuê xe.";
                return RedirectToAction(nameof(Profile));
            }

            input.CustomerId = userId;
            if (input.EmployeeId <= 0)
            {
                var employees = await _rentalRepository.GetUsersByRoleAsync("EMPLOYEE");
                input.EmployeeId = employees.FirstOrDefault()?.UserId ?? 3;
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Đặt thuê xe thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thuê xe", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaymentInputModel input, int? redirectContractId)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Dữ liệu thanh toán không hợp lệ.";
            if (redirectContractId.HasValue && redirectContractId.Value > 0)
            {
                return RedirectToAction("Details", "Rental", new { contractId = redirectContractId.Value });
            }

            return RedirectToAction(nameof(Contracts));
        }

        try
        {
            await _rentalRepository.MakePaymentAsync(input);
            TempData["Success"] = "Thanh toán thành công. Doanh thu xe và tổng doanh thu admin đã được cập nhật.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thanh toán", ex);
        }

        if (redirectContractId.HasValue && redirectContractId.Value > 0)
        {
            return RedirectToAction("Details", "Rental", new { contractId = redirectContractId.Value });
        }

        return RedirectToAction(nameof(Contracts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFavorite(int vehicleId)
    {
        return await ToggleFavorite(vehicleId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            TempData["Error"] = "Xe không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ToggleFavoriteVehicleAsync(User.GetUserId(), vehicleId);
            TempData["Success"] = "Đã cập nhật danh sách yêu thích.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cập nhật danh sách yêu thích", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(CustomerMessageInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nội dung tin nhắn không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.SendMessageToAdminAsync(User.GetUserId(), input.Content);
            TempData["Success"] = "Đã gửi tin nhắn cho Admin.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gửi tin nhắn", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(VehicleReviewInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nội dung review không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.AddVehicleReviewAsync(User.GetUserId(), input);
            TempData["Success"] = "Cảm ơn bạn đã gửi review cho chuyến đi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gửi review", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(VehicleReviewInputModel input)
    {
        // Alias for SubmitReview to match View form call
        return await SubmitReview(input);
    }

    [HttpGet]
    public async Task<IActionResult> ShowContract(int contractId)
    {
        if (contractId <= 0)
        {
            return BadRequest("Hợp đồng không hợp lệ.");
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(contractId);
            if (contract is null)
            {
                return NotFound("Không tìm thấy hợp đồng.");
            }

            var userId = User.GetUserId();
            if (contract.CustomerId != userId)
            {
                return Forbid();
            }

            return Json(contract);
        }
        catch (OracleException ex)
        {
            return StatusCode(500, new { error = BuildOracleErrorMessage("lấy thông tin hợp đồng", ex) });
        }
    }

    [HttpGet]
    public async Task<IActionResult> VehicleDetails(int id, DateTime? checkIn, DateTime? checkOut)
    {
        if (id <= 0)
        {
            TempData["Error"] = "ID xe không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = User.GetUserId();
            var vehicles = await _rentalRepository.GetVehiclesForCustomerAsync(userId);
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == id);

            if (vehicle is null)
            {
                TempData["Error"] = "Không tìm thấy xe đã chọn.";
                return RedirectToAction(nameof(Index));
            }

            var rentalDates = await _rentalRepository.GetVehicleRentalDatesAsync(id);
            var selectedCheckIn = checkIn?.Date;
            var selectedCheckOut = checkOut?.Date;

            bool? isAvailableForSelectedDates = null;
            decimal? estimatedRentalCost = null;
            int? estimatedRentalDays = null;
            var availabilityNote = string.Empty;

            if (selectedCheckIn.HasValue && selectedCheckOut.HasValue && selectedCheckOut.Value >= selectedCheckIn.Value)
            {
                isAvailableForSelectedDates = !rentalDates.Any(range =>
                    range.StartDate.Date <= selectedCheckOut.Value
                    && range.EndDate.Date.AddDays(1) >= selectedCheckIn.Value);

                estimatedRentalDays = Math.Max(1, (selectedCheckOut.Value - selectedCheckIn.Value).Days);
                estimatedRentalCost = await _rentalRepository.CalculateRentalCostAsync(vehicle.PricePerDay, selectedCheckIn.Value, selectedCheckOut.Value);
                availabilityNote = isAvailableForSelectedDates.Value
                    ? "Xe đang rảnh trong khoảng ngày bạn chọn."
                    : "Xe không rảnh trong khoảng ngày bạn chọn. Vui lòng đổi ngày khác.";
            }
            else if (selectedCheckIn.HasValue ^ selectedCheckOut.HasValue)
            {
                availabilityNote = "Vui lòng chọn đầy đủ cả ngày nhận và ngày trả để kiểm tra lịch và tạm tính chi phí.";
            }
            else if (selectedCheckIn.HasValue && selectedCheckOut.HasValue && selectedCheckOut.Value < selectedCheckIn.Value)
            {
                availabilityNote = "Ngay tra phai lon hon hoac bang ngay nhan.";
            }

            var model = new VehicleDetailsViewModel
            {
                VehicleId = vehicle.VehicleId,
                VehicleName = vehicle.VehicleName,
                BrandName = vehicle.BrandName,
                TypeName = vehicle.TypeName,
                PricePerDay = vehicle.PricePerDay,
                AmenitiesText = vehicle.AmenitiesText,
                PrimaryImageUrl = vehicle.PrimaryImageUrl,
                IsFavorite = vehicle.IsFavorite,
                RentalDates = rentalDates,
                SelectedCheckInDate = selectedCheckIn,
                SelectedCheckOutDate = selectedCheckOut,
                IsAvailableForSelectedDates = isAvailableForSelectedDates,
                AvailabilityNote = availabilityNote,
                EstimatedRentalCost = estimatedRentalCost,
                EstimatedRentalDays = estimatedRentalDays
            };

            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tải chi tiết xe", ex);
            return RedirectToAction(nameof(Index));
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

    private async Task<CustomerDashboardViewModel> BuildDashboardAsync(
        int userId,
        string? keyword,
        IReadOnlyCollection<string> selectedAmenityCodes,
        int? selectedBrandId,
        int? selectedTypeId,
        DateTime? checkInDate,
        DateTime? checkOutDate)
    {
        var profileTask = _rentalRepository.GetUserProfileAsync(userId);
        var brandsTask = _rentalRepository.GetBrandsAsync();
        var typesTask = _rentalRepository.GetTypesAsync();
        var amenitiesTask = _rentalRepository.GetAmenityOptionsAsync();
        var notificationsTask = _rentalRepository.GetNotificationsForUserAsync(userId);
        var vehiclesTask = _rentalRepository.GetVehiclesForCustomerAsync(userId, keyword, selectedAmenityCodes, checkInDate, checkOutDate);
        var favoritesTask = _rentalRepository.GetFavoriteVehiclesByCustomerAsync(userId);
        var messagesTask = _rentalRepository.GetMessagesForCustomerAsync(userId);
        var reviewableTask = _rentalRepository.GetReviewableContractsByCustomerAsync(userId);
        var contractsTask = _rentalRepository.GetContractsByCustomerAsync(userId);
        var pendingContractsTask = _rentalRepository.GetPendingContractsByCustomerAsync(userId);
        var verificationStatusTask = _rentalRepository.GetCustomerVerificationStatusAsync(userId);

        try
        {
            await Task.WhenAll(
                brandsTask,
                typesTask,
                amenitiesTask,
                notificationsTask,
                vehiclesTask,
                favoritesTask,
                messagesTask,
                reviewableTask,
                contractsTask,
                pendingContractsTask,
                verificationStatusTask);
        }
        catch (OracleException ex) when (ex.Number == 942 || ex.Number == 904)
        {
            // Table/view/function missing - use fallback values for failed tasks
        }
        catch
        {
            // Other errors - use fallback values
        }

        var profile = await profileTask;
        var brandOptions = TryGetResult(brandsTask, []);
        var typeOptions = TryGetResult(typesTask, []);

        var selectedBrandName = selectedBrandId.HasValue
            ? brandOptions.FirstOrDefault(x => x.BrandId == selectedBrandId.Value)?.BrandName
            : null;

        var selectedTypeName = selectedTypeId.HasValue
            ? typeOptions.FirstOrDefault(x => x.TypeId == selectedTypeId.Value)?.TypeName
            : null;

        var filteredVehicles = TryGetResult(vehiclesTask, []);

        if (checkInDate.HasValue && checkOutDate.HasValue && filteredVehicles.Count == 0)
        {
            // Fallback: still show matching vehicles even when availability-filtered sources return empty.
            var fallbackVehicles = await _rentalRepository.GetVehiclesForCustomerAsync(
                userId,
                keyword,
                selectedAmenityCodes,
                null,
                null);
            filteredVehicles = fallbackVehicles.ToList();
        }

        if (!string.IsNullOrWhiteSpace(selectedBrandName))
        {
            filteredVehicles = filteredVehicles
                .Where(v => string.Equals(v.BrandName, selectedBrandName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(selectedTypeName))
        {
            filteredVehicles = filteredVehicles
                .Where(v => string.Equals(v.TypeName, selectedTypeName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (checkInDate.HasValue && checkOutDate.HasValue)
        {
            var estimatedDays = Math.Max(1, (checkOutDate.Value.Date - checkInDate.Value.Date).Days);
            foreach (var vehicle in filteredVehicles)
            {
                var rentalDates = await _rentalRepository.GetVehicleRentalDatesAsync(vehicle.VehicleId);
                vehicle.IsAvailableForSelectedDates = !rentalDates.Any(range =>
                    range.StartDate.Date <= checkOutDate.Value.Date
                    && range.EndDate.Date.AddDays(1) >= checkInDate.Value.Date);

                vehicle.EstimatedRentalDays = estimatedDays;
                vehicle.EstimatedRentalCost = await _rentalRepository.CalculateRentalCostAsync(vehicle.PricePerDay, checkInDate.Value, checkOutDate.Value);
                vehicle.AvailabilityNote = vehicle.IsAvailableForSelectedDates
                    ? "Xe đang rảnh trong khoảng ngày đã chọn."
                    : "Xe không rảnh trong khoảng ngày đã chọn.";
            }
        }

        return new CustomerDashboardViewModel
        {
            UserId = userId,
            FullName = profile.FullName,
            Email = profile.Email,
            Phone = profile.Phone,
            SearchKeyword = keyword?.Trim() ?? string.Empty,
            BrandOptions = brandOptions,
            TypeOptions = typeOptions,
            AmenityOptions = TryGetResult(amenitiesTask, []),
            SelectedAmenityCodes = selectedAmenityCodes.ToArray(),
            SelectedBrandId = selectedBrandId,
            SelectedTypeId = selectedTypeId,
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            Notifications = TryGetResult(notificationsTask, []),
            Vehicles = filteredVehicles,
            FavoriteVehicles = TryGetResult(favoritesTask, []),
            Messages = TryGetResult(messagesTask, []),
            ReviewableContracts = TryGetResult(reviewableTask, []),
            Contracts = TryGetResult(contractsTask, []),
            PendingContracts = TryGetResult(pendingContractsTask, []),
            VerificationStatus = TryGetResult(verificationStatusTask, new())
        };
    }

    private static T TryGetResult<T>(Task<T> task, T defaultValue)
    {
        try
        {
            return task.IsCompletedSuccessfully ? task.Result : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
