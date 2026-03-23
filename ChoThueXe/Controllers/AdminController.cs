using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace ChoThueXe.Controllers;

[Authorize(Roles = "ADMIN")]
public class AdminController : Controller
{
    private readonly IRentalRepository _rentalRepository;

    public AdminController(IRentalRepository rentalRepository)
    {
        _rentalRepository = rentalRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
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

        return View(new AdminDashboardViewModel
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
            Contracts = contractsTask.Result,
            VehicleOccupancies = occupancyTask.Result,
            RevenueByVehicle = revenueByVehicleTask.Result,
            RevenueByAccount = revenueByAccountTask.Result,
            TopRentedVehicles = topRentedTask.Result
        });
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
            TempData["Error"] = $"Loi duyet giay to: {ex.Message}";
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
            TempData["Error"] = $"Loi them xe: {ex.Message}";
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
            TempData["Error"] = $"Loi phan hoi tin nhan: {ex.Message}";
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
            TempData["Error"] = $"Loi duyet bo giay to: {ex.Message}";
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
            TempData["Error"] = $"Loi duyet cap nhat thong tin: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
