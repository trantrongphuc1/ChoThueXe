using ChoThueXe.Models.Rental;

namespace ChoThueXe.Models.Portal;

public class AdminRevenueReportViewModel
{
    public IReadOnlyList<RevenueViewModel> RevenueByVehicle { get; init; } = [];
    public IReadOnlyList<RevenueByAccountViewModel> RevenueByAccount { get; init; } = [];
    public IReadOnlyList<RevenuePeriodPointViewModel> RevenueByMonth { get; init; } = [];
    public IReadOnlyList<RevenuePeriodPointViewModel> RevenueByWeek { get; init; } = [];
    public IReadOnlyList<TopCustomerRentalViewModel> TopCustomersByRentals { get; init; } = [];
}
