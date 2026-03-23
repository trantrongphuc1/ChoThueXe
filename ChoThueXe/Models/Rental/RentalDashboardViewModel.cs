namespace ChoThueXe.Models.Rental;

public class RentalDashboardViewModel
{
    public IReadOnlyList<VehicleDetailViewModel> Vehicles { get; init; } = [];
    public IReadOnlyList<ContractFullViewModel> Contracts { get; init; } = [];
    public IReadOnlyList<RevenueViewModel> Revenue { get; init; } = [];
    public IReadOnlyList<UserOptionViewModel> Users { get; init; } = [];
    public IReadOnlyList<PendingContractViewModel> PendingContracts { get; init; } = [];
}
