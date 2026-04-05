using ChoThueXe.Models.Rental;

namespace ChoThueXe.Models.Portal;

public class CustomerDashboardViewModel
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;

    public IReadOnlyList<VehicleDetailViewModel> Vehicles { get; init; } = [];
    public IReadOnlyList<VehicleDetailViewModel> FavoriteVehicles { get; init; } = [];
    public IReadOnlyList<BrandOptionViewModel> BrandOptions { get; init; } = [];
    public IReadOnlyList<TypeOptionViewModel> TypeOptions { get; init; } = [];
    public IReadOnlyList<AmenityOptionViewModel> AmenityOptions { get; init; } = [];
    public IReadOnlyList<string> SelectedAmenityCodes { get; init; } = [];
    public int? SelectedBrandId { get; init; }
    public int? SelectedTypeId { get; init; }
    public DateTime? CheckInDate { get; init; }
    public DateTime? CheckOutDate { get; init; }
    public string SearchKeyword { get; init; } = string.Empty;
    public IReadOnlyList<NotificationViewModel> Notifications { get; init; } = [];
    public IReadOnlyList<SupportMessageViewModel> Messages { get; init; } = [];
    public IReadOnlyList<ReviewableContractViewModel> ReviewableContracts { get; init; } = [];
    public IReadOnlyList<ContractFullViewModel> Contracts { get; init; } = [];
    public IReadOnlyList<PendingContractViewModel> PendingContracts { get; init; } = [];
    public CustomerVerificationStatusViewModel VerificationStatus { get; init; } = new();
}
