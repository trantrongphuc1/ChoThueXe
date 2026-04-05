namespace ChoThueXe.Models.Rental;

public class RevenueViewModel
{
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int TotalContractsCompleted { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgRentalValue { get; set; }
    public int TotalRentalDays { get; set; }
    public decimal RevenuePerDay { get; set; }
}
