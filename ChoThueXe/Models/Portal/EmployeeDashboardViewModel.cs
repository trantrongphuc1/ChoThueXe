using ChoThueXe.Models.Rental;

namespace ChoThueXe.Models.Portal;

public class EmployeeDashboardViewModel
{
    public IReadOnlyList<CustomerForEmployeeViewModel> Customers { get; init; } = [];
    public IReadOnlyList<PendingContractViewModel> PendingContracts { get; init; } = [];
    public IReadOnlyList<VehicleDetailViewModel> Vehicles { get; init; } = [];
}
