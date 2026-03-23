using ChoThueXe.Data;
using ChoThueXe.Infrastructure;
using ChoThueXe.Models.Portal;
using ChoThueXe.Models.Rental;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace ChoThueXe.Controllers;

[Authorize(Roles = "CUSTOMER,EMPLOYEE,ADMIN")]
public class CustomerController : Controller
{
    private readonly IRentalRepository _rentalRepository;

    public CustomerController(IRentalRepository rentalRepository)
    {
        _rentalRepository = rentalRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string[]? amenities)
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
            TempData["Error"] = $"Loi cap nhat profile: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitDocument(SubmitDocumentInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thong tin giay to khong hop le.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _rentalRepository.SubmitUserDocumentAsync(User.GetUserId(), input);
            TempData["Success"] = "Da gui giay to, vui long doi Admin duyet.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi gui giay to: {ex.Message}";
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
            TempData["Error"] = $"Loi preview chi phi: {ex.Message}";
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

            input.CustomerId = userId;
            if (input.EmployeeId <= 0)
            {
                var employees = await _rentalRepository.GetUsersByRoleAsync("EMPLOYEE");
                input.EmployeeId = employees.FirstOrDefault()?.UserId ?? 3;
            }

            await _rentalRepository.RentVehicleAsync(input);
            TempData["Success"] = "Dat thue xe thanh cong.";
        }
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi thue xe: {ex.Message}";
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
        catch (OracleException ex)
        {
            TempData["Error"] = $"Loi thanh toan: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
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
            TempData["Error"] = $"Loi cap nhat yeu thich: {ex.Message}";
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
            TempData["Error"] = $"Loi gui tin nhan: {ex.Message}";
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
            TempData["Error"] = $"Loi gui review: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
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

        await Task.WhenAll(
            amenitiesTask,
            notificationsTask,
            vehiclesTask,
            favoritesTask,
            messagesTask,
            reviewableTask,
            contractsTask,
            pendingContractsTask);
        var profile = await profileTask;

        return new CustomerDashboardViewModel
        {
            UserId = userId,
            FullName = profile.FullName,
            Email = profile.Email,
            Phone = profile.Phone,
            SearchKeyword = keyword?.Trim() ?? string.Empty,
            AmenityOptions = amenitiesTask.Result,
            SelectedAmenityCodes = selectedAmenityCodes.ToArray(),
            Notifications = notificationsTask.Result,
            Vehicles = vehiclesTask.Result,
            FavoriteVehicles = favoritesTask.Result,
            Messages = messagesTask.Result,
            ReviewableContracts = reviewableTask.Result,
            Contracts = contractsTask.Result,
            PendingContracts = pendingContractsTask.Result
        };
    }
}
