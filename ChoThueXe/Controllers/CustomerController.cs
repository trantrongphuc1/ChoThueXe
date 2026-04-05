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
    public async Task<IActionResult> Index(string? q, string[]? amenities)
    {
        try
        {
            var userId = User.GetUserId();
            var selectedAmenities = (amenities ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var model = await BuildDashboardAsync(userId, q, selectedAmenities);
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
                AmenityOptions = [],
                VerificationStatus = new()
            };
            return View(model);
        }
        catch (Exception)
        {
            TempData["Error"] = "Khong the tai dashboard luc nay. Vui long thu lai.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        try
        {
            var userId = User.GetUserId();
            var profile = await _rentalRepository.GetUserProfileAsync(userId);

            var model = new UpdateProfileInputModel
            {
                FullName = profile.FullName,
                Phone = profile.Phone
            };

            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tai ho so", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocumentFile(IFormFile file, string? category)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest(new { error = "Vui long chon file de tai len." });
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Dinh dang file khong hop le. Chi ho tro JPG, PNG, PDF." });
        }

        const long maxSizeInBytes = 5 * 1024 * 1024;
        if (file.Length > maxSizeInBytes)
        {
            return BadRequest(new { error = "File vuot qua 5MB. Vui long chon file nho hon." });
        }

        var webRootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            return StatusCode(500, new { error = "Khong tim thay thu muc luu file tren he thong." });
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
            TempData["Error"] = "Thong tin cap nhat khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.SubmitProfileUpdateRequestAsync(User.GetUserId(), input.FullName, input.Phone);
            TempData["Success"] = "Da gui yeu cau sua thong tin. Admin se duyet truoc khi cap nhat.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cap nhat thong tin", ex);
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
            TempData["Error"] = "Mat khau nhap lai khong khop hoac du lieu khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _authRepository.ChangePasswordAsync(User.GetUserId(), currentPassword, newPassword);
            TempData["Success"] = "Da thay doi mat khau thanh cong.";
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
            TempData["Error"] = BuildOracleErrorMessage("doi mat khau", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDocument(SubmitDocumentInputModel input)
    {
        if (string.IsNullOrWhiteSpace(input.DocType))
        {
            TempData["Error"] = "Vui long chon loai giay to.";
            return RedirectToAction(nameof(Profile));
        }

        if (string.IsNullOrWhiteSpace(input.FileUrl))
        {
            TempData["Error"] = "Vui long tai file len server truoc khi gui giay to.";
            return RedirectToAction(nameof(Profile));
        }

        try
        {
            await _rentalRepository.SubmitUserDocumentAsync(User.GetUserId(), input);
            TempData["Success"] = "Da gui giay to, vui long doi Admin duyet.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gui giay to", ex);
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDriveLicense(SubmitDriveLicenseInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin bang lai khong hop le.";
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

                TempData["Success"] = "Da gui bang lai (thong tin + file), vui long doi Admin duyet.";
            }
            else
            {
                TempData["Success"] = "Da gui thong tin bang lai. Hay tai them file bang lai de Admin duyet nhanh hon.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gui bang lai", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(RentVehicleInputModel input)
    {
        if (input.StartDate == default || input.EndDate == default || input.StartDate > input.EndDate)
        {
            TempData["Error"] = "Ngay thue khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesForCustomerAsync(User.GetUserId());
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == input.VehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Khong tim thay xe da chon.";
                return RedirectToAction(nameof(Index));
            }

            var estimate = await _rentalRepository.CalculateRentalCostAsync(vehicle.PricePerDay, input.StartDate, input.EndDate);
            TempData["Info"] = $"Chi phi du kien: {estimate:N0} VND";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("preview chi phi", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rent(RentVehicleInputModel input)
    {
        if (input.StartDate == default || input.EndDate == default)
        {
            TempData["Error"] = "Ngay thue khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = User.GetUserId();
            var isVerified = await _rentalRepository.IsUserVerifiedAsync(userId);
            if (!isVerified)
            {
                TempData["Error"] = "Ban chua duoc xac minh giay to. Hay upload CCCD/Bang lai xe.";
                return RedirectToAction(nameof(Index));
            }

            var verification = await _rentalRepository.GetCustomerVerificationStatusAsync(userId);
            var hasApprovedCccd = verification.HasCccd && string.Equals(verification.CccdStatus, "APPROVED", StringComparison.OrdinalIgnoreCase);
            var hasApprovedDriverLicense = verification.HasDriverLicense && string.Equals(verification.DriverLicenseStatus, "APPROVED", StringComparison.OrdinalIgnoreCase);
            if (!hasApprovedCccd || !hasApprovedDriverLicense)
            {
                TempData["Error"] = "Can duoc duyet day du CCCD va bang lai xe truoc khi thue xe.";
                return RedirectToAction(nameof(Profile));
            }

            input.CustomerId = userId;
            if (input.EmployeeId <= 0)
            {
                var employees = await _rentalRepository.GetUsersByRoleAsync("EMPLOYEE");
                input.EmployeeId = employees.FirstOrDefault()?.UserId ?? 3;
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Dat thue xe thanh cong.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thue xe", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PaymentInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Du lieu thanh toan khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.MakePaymentAsync(input);
            TempData["Success"] = "Thanh toan thanh cong.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("thanh toan", ex);
        }

        return RedirectToAction(nameof(Index));
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
            TempData["Error"] = "Xe khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ToggleFavoriteVehicleAsync(User.GetUserId(), vehicleId);
            TempData["Success"] = "Da cap nhat danh sach yeu thich.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cap nhat danh sach yeu thich", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(CustomerMessageInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Noi dung tin nhan khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.SendMessageToAdminAsync(User.GetUserId(), input.Content);
            TempData["Success"] = "Da gui tin nhan cho Admin.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gui tin nhan", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(VehicleReviewInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Noi dung review khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.AddVehicleReviewAsync(User.GetUserId(), input);
            TempData["Success"] = "Cam on ban da gui review cho chuyen di.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("gui review", ex);
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
            return BadRequest("Hop dong khong hop le.");
        }

        try
        {
            var contract = await _rentalRepository.GetContractByIdAsync(contractId);
            if (contract is null)
            {
                return NotFound("Khong tim thay hop dong.");
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
            return StatusCode(500, new { error = BuildOracleErrorMessage("lay thong tin hop dong", ex) });
        }
    }

    [HttpGet]
    public async Task<IActionResult> VehicleDetails(int id)
    {
        if (id <= 0)
        {
            TempData["Error"] = "ID xe khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var userId = User.GetUserId();
            var vehicles = await _rentalRepository.GetVehiclesForCustomerAsync(userId);
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == id);

            if (vehicle is null)
            {
                TempData["Error"] = "Khong tim thay xe da chon.";
                return RedirectToAction(nameof(Index));
            }

            var rentalDates = await _rentalRepository.GetVehicleRentalDatesAsync(id);

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
                RentalDates = rentalDates
            };

            return View(model);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("tai chi tiet xe", ex);
            return RedirectToAction(nameof(Index));
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

    private async Task<CustomerDashboardViewModel> BuildDashboardAsync(int userId, string? keyword, IReadOnlyCollection<string> selectedAmenityCodes)
    {
        var profileTask = _rentalRepository.GetUserProfileAsync(userId);
        var amenitiesTask = _rentalRepository.GetAmenityOptionsAsync();
        var notificationsTask = _rentalRepository.GetNotificationsForUserAsync(userId);
        var vehiclesTask = _rentalRepository.GetVehiclesForCustomerAsync(userId, keyword, selectedAmenityCodes);
        var favoritesTask = _rentalRepository.GetFavoriteVehiclesByCustomerAsync(userId);
        var messagesTask = _rentalRepository.GetMessagesForCustomerAsync(userId);
        var reviewableTask = _rentalRepository.GetReviewableContractsByCustomerAsync(userId);
        var contractsTask = _rentalRepository.GetContractsByCustomerAsync(userId);
        var pendingContractsTask = _rentalRepository.GetPendingContractsByCustomerAsync(userId);
        var verificationStatusTask = _rentalRepository.GetCustomerVerificationStatusAsync(userId);

        try
        {
            await Task.WhenAll(
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

        return new CustomerDashboardViewModel
        {
            UserId = userId,
            FullName = profile.FullName,
            Email = profile.Email,
            Phone = profile.Phone,
            SearchKeyword = keyword?.Trim() ?? string.Empty,
            AmenityOptions = TryGetResult(amenitiesTask, []),
            SelectedAmenityCodes = selectedAmenityCodes.ToArray(),
            Notifications = TryGetResult(notificationsTask, []),
            Vehicles = TryGetResult(vehiclesTask, []),
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
