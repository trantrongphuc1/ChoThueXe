using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

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
    public async Task<IActionResult> Index(string? contractStatus, string? contractQ)
    {
        try
        {
            var docsTask = _rentalRepository.GetPendingDocumentsAsync();
            var verificationTask = _rentalRepository.GetPendingVerificationsAsync();
            var profileUpdateTask = _rentalRepository.GetPendingProfileUpdateRequestsAsync();
            var brandsTask = _rentalRepository.GetBrandsAsync();
            var typesTask = _rentalRepository.GetTypesAsync();
            var amenitiesTask = _rentalRepository.GetAmenityOptionsAsync();
            var vehiclesTask = _rentalRepository.GetVehiclesAsync();
            var messagesTask = _rentalRepository.GetMessagesForAdminAsync();
            var accountsTask = _rentalRepository.GetAdminAccountsAsync();
            var contractsTask = _rentalRepository.GetContractsAsync();
            var occupancyTask = _rentalRepository.GetAdminVehicleOccupanciesAsync();
            var revenueByVehicleTask = _rentalRepository.GetRevenueAsync();
            var revenueByAccountTask = _rentalRepository.GetRevenueByAccountAsync();
            var topRentedTask = _rentalRepository.GetTopRentedVehiclesAsync();

            await Task.WhenAll(
                docsTask,
                verificationTask,
                profileUpdateTask,
                brandsTask,
                typesTask,
                amenitiesTask,
                vehiclesTask,
                messagesTask,
                accountsTask,
                contractsTask,
                occupancyTask,
                revenueByVehicleTask,
                revenueByAccountTask,
                topRentedTask);

            var selectedStatus = (contractStatus ?? string.Empty).Trim();
            var keyword = (contractQ ?? string.Empty).Trim();
            IEnumerable<Models.Rental.ContractFullViewModel> filteredContracts = contractsTask.Result;

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

            ViewData["ContractStatusFilter"] = selectedStatus;
            ViewData["ContractKeywordFilter"] = keyword;

            return View("DashboardMinimal", new AdminDashboardViewModel
            {
                PendingDocuments = docsTask.Result,
                PendingVerifications = verificationTask.Result,
                PendingProfileUpdates = profileUpdateTask.Result,
                Brands = brandsTask.Result,
                Types = typesTask.Result,
                AmenityOptions = amenitiesTask.Result,
                Vehicles = vehiclesTask.Result,
                Messages = messagesTask.Result,
                Accounts = accountsTask.Result,
                Contracts = filteredContracts.ToList(),
                VehicleOccupancies = occupancyTask.Result,
                RevenueByVehicle = revenueByVehicleTask.Result,
                RevenueByAccount = revenueByAccountTask.Result,
                TopRentedVehicles = topRentedTask.Result
            });
        }
        catch (Exception)
        {
            TempData["Error"] = "Khong the tai dashboard quan tri luc nay. Vui long thu lai.";
            return RedirectToAction("Login", "Auth");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDocument(int documentId)
    {
        if (documentId <= 0)
        {
            TempData["Error"] = "Document id khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ApproveDocumentAsync(documentId, User.GetUserId());
            TempData["Success"] = "Da duyet giay to thanh cong.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyet giay to", ex);
        }

        return RedirectToAction(nameof(Index));
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
            TempData["Error"] = "Thong tin xe khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.AddVehicleAsync(input);
            TempData["Success"] = "Admin da them xe thanh cong va gui thong bao den khach hang.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"{BuildOracleErrorMessage("them xe", ex)} (Ma loi ORA: {ex.Number})";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditVehicle(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            TempData["Error"] = "Xe khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var vehicles = await _rentalRepository.GetVehiclesAsync();
            var vehicle = vehicles.FirstOrDefault(v => v.VehicleId == vehicleId);
            if (vehicle is null)
            {
                TempData["Error"] = "Khong tim thay xe.";
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new CreateVehicleInputModel
            {
                VehicleId = vehicle.VehicleId,
                VehicleName = vehicle.VehicleName,
                BrandId = vehicle.BrandId,
                TypeId = vehicle.TypeId,
                LicensePlate = vehicle.LicensePlate,
                PricePerDay = vehicle.PricePerDay,
                OwnerId = vehicle.OwnerId,
                Status = vehicle.Status
            };

            ViewBag.Brands = await _rentalRepository.GetBrandsAsync();
            ViewBag.Types = await _rentalRepository.GetTypesAsync();
            ViewBag.Amenities = await _rentalRepository.GetAmenityOptionsAsync();

            return View(viewModel);
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("lay thong tin xe", ex);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVehicle(CreateVehicleInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin xe khong hop le.";
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
            TempData["Success"] = "Cap nhat thong tin xe thanh cong.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("cap nhat xe", ex);
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
            TempData["Error"] = "Noi dung phan hoi khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ReplyMessageAsync(input.MessageId, User.GetUserId(), input.ReplyContent);
            TempData["Success"] = "Da gui phan hoi cho khach hang.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("phan hoi tin nhan", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewDocuments(ReviewDocumentsInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Du lieu duyet giay to khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ReviewUserDocumentsAsync(input.UserId, User.GetUserId(), input.IsMatched);
            TempData["Success"] = input.IsMatched
                ? "Da duyet CCCD va bang lai, thong tin khop."
                : "Da tu choi bo giay to do khong khop.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyet bo giay to", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewProfileUpdate(ReviewProfileUpdateRequestInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Du lieu duyet cap nhat thong tin khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.ReviewProfileUpdateRequestAsync(input.RequestId, User.GetUserId(), input.IsApproved);
            TempData["Success"] = input.IsApproved
                ? "Da duyet yeu cau cap nhat thong tin ca nhan."
                : "Da tu choi yeu cau cap nhat thong tin ca nhan.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (OracleException ex)
        {
            TempData["Error"] = BuildOracleErrorMessage("duyet cap nhat thong tin", ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadVehicleImages(List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new { error = "Vui long chon it nhat 1 anh." });
        }

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        const long maxSizeInBytes = 8 * 1024 * 1024;

        var webRootPath = _webHostEnvironment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            return StatusCode(500, new { error = "Khong tim thay thu muc luu anh tren he thong." });
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
                return BadRequest(new { error = $"File '{file.FileName}' khong hop le. Chi ho tro JPG, PNG, WEBP." });
            }

            if (file.Length > maxSizeInBytes)
            {
                return BadRequest(new { error = $"File '{file.FileName}' vuot qua 8MB." });
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
            return BadRequest(new { error = "Khong co file hop le de tai len." });
        }

        return Json(new { urls });
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
