using ChoThueXe.Models.Rental;

namespace ChoThueXe.Models.Portal;

public class AdminDashboardViewModel
{
    public IReadOnlyList<PendingDocumentViewModel> PendingDocuments { get; init; } = [];
    public IReadOnlyList<PendingVerificationViewModel> PendingVerifications { get; init; } = [];
    public IReadOnlyList<PendingProfileUpdateRequestViewModel> PendingProfileUpdates { get; init; } = [];
    public IReadOnlyList<BrandOptionViewModel> Brands { get; init; } = [];
    public IReadOnlyList<TypeOptionViewModel> Types { get; init; } = [];
    public IReadOnlyList<AmenityOptionViewModel> AmenityOptions { get; init; } = [];
    public IReadOnlyList<VehicleDetailViewModel> Vehicles { get; init; } = [];
    public IReadOnlyList<SupportMessageViewModel> Messages { get; init; } = [];
    public IReadOnlyList<AdminAccountManagementViewModel> Accounts { get; init; } = [];
    public IReadOnlyList<ContractFullViewModel> Contracts { get; init; } = [];
    public IReadOnlyList<AdminVehicleOccupancyViewModel> VehicleOccupancies { get; init; } = [];
    public IReadOnlyList<RevenueViewModel> RevenueByVehicle { get; init; } = [];
    public IReadOnlyList<RevenueByAccountViewModel> RevenueByAccount { get; init; } = [];
    public IReadOnlyList<TopRentedVehicleViewModel> TopRentedVehicles { get; init; } = [];
}
