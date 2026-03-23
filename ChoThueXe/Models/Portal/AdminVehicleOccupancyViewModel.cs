namespace ChoThueXe.Models.Portal;

public class AdminVehicleOccupancyViewModel
{
    public int VehicleId { get; init; }
    public string VehicleName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Occupancy { get; init; } = string.Empty;
}
