using ChoThueXe.Models.Rental;
using ChoThueXe.Models.Portal;

namespace ChoThueXe.Data;

public interface IRentalRepository
{
    Task<IReadOnlyList<VehicleDetailViewModel>> GetVehiclesAsync(string? keyword = null, IReadOnlyCollection<string>? amenityCodes = null);
    Task<IReadOnlyList<VehicleDetailViewModel>> GetVehiclesForCustomerAsync(int customerId, string? keyword = null, IReadOnlyCollection<string>? amenityCodes = null);
    Task<IReadOnlyList<VehicleDetailViewModel>> GetFavoriteVehiclesByCustomerAsync(int customerId);
    Task<IReadOnlyList<ContractFullViewModel>> GetContractsAsync();
    Task<IReadOnlyList<RevenueViewModel>> GetRevenueAsync();
    Task<IReadOnlyList<UserOptionViewModel>> GetUsersAsync();
    Task<IReadOnlyList<UserOptionViewModel>> GetUsersByRoleAsync(string roleName);
    Task<(string FullName, string Email, string Phone)> GetUserProfileAsync(int userId);
    Task<IReadOnlyList<PendingContractViewModel>> GetPendingContractsAsync();
    Task<IReadOnlyList<PendingContractViewModel>> GetPendingContractsByCustomerAsync(int customerId);
    Task<IReadOnlyList<ContractFullViewModel>> GetContractsByCustomerAsync(int customerId);
    Task<IReadOnlyList<CustomerForEmployeeViewModel>> GetCustomersForEmployeeAsync();
    Task<IReadOnlyList<PendingDocumentViewModel>> GetPendingDocumentsAsync();
    Task<IReadOnlyList<PendingVerificationViewModel>> GetPendingVerificationsAsync();
    Task<IReadOnlyList<BrandOptionViewModel>> GetBrandsAsync();
    Task<IReadOnlyList<TypeOptionViewModel>> GetTypesAsync();
    Task<IReadOnlyList<AmenityOptionViewModel>> GetAmenityOptionsAsync();
    Task<IReadOnlyList<NotificationViewModel>> GetNotificationsForUserAsync(int userId);
    Task<IReadOnlyList<SupportMessageViewModel>> GetMessagesForAdminAsync();
    Task<IReadOnlyList<SupportMessageViewModel>> GetMessagesForCustomerAsync(int customerId);
    Task<IReadOnlyList<ReviewableContractViewModel>> GetReviewableContractsByCustomerAsync(int customerId);
    Task<IReadOnlyList<AdminAccountManagementViewModel>> GetAdminAccountsAsync();
    Task<IReadOnlyList<AdminVehicleOccupancyViewModel>> GetAdminVehicleOccupanciesAsync();
    Task<IReadOnlyList<RevenueByAccountViewModel>> GetRevenueByAccountAsync();
    Task<IReadOnlyList<TopRentedVehicleViewModel>> GetTopRentedVehiclesAsync();

    Task<bool> IsUserVerifiedAsync(int userId);
    Task<decimal> CalculateRentalCostAsync(decimal pricePerDay, DateTime startDate, DateTime endDate);

    Task UpdateUserProfileAsync(int userId, string fullName, string phone);
    Task SubmitProfileUpdateRequestAsync(int userId, string fullName, string phone);
    Task<IReadOnlyList<PendingProfileUpdateRequestViewModel>> GetPendingProfileUpdateRequestsAsync();
    Task ReviewProfileUpdateRequestAsync(int requestId, int approvedBy, bool isApproved);
    Task SubmitUserDocumentAsync(int userId, SubmitDocumentInputModel input);
    Task ApproveDocumentAsync(int documentId, int approvedBy);
    Task ReviewUserDocumentsAsync(int userId, int approvedBy, bool isMatched);
    Task AddVehicleAsync(CreateVehicleInputModel input);
    Task ToggleFavoriteVehicleAsync(int customerId, int vehicleId);
    Task SendMessageToAdminAsync(int customerId, string content);
    Task ReplyMessageAsync(int messageId, int adminId, string replyContent);
    Task BroadcastVehicleNotificationAsync(int adminId, int vehicleId, string vehicleName);
    Task AddVehicleReviewAsync(int customerId, VehicleReviewInputModel input);

    Task CreateContractDraftAsync(int customerId, int employeeId);
    Task RentVehicleAsync(RentVehicleInputModel input);
    Task MakePaymentAsync(PaymentInputModel input);

    Task<IReadOnlyList<DriveLicenseViewModel>> GetDriveLicensesAsync(int userId);
    Task SubmitDriveLicenseAsync(int userId, string licenseNumber, DateTime issuedAt, DateTime expireAt, string issuedBy);

    Task LogActivityAsync(int? userId, string action, string details);
}
