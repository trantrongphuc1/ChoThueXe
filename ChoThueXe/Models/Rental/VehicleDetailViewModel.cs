namespace ChoThueXe.Models.Rental;

public class VehicleDetailViewModel
{
    public int VehicleId { get; set; }
    public int BrandId { get; set; }
    public int TypeId { get; set; }
    public int OwnerId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public decimal PricePerDay { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AmenitiesText { get; set; } = string.Empty;
    public string PrimaryImageUrl { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public decimal? EstimatedRentalCost { get; set; }
    public int? EstimatedRentalDays { get; set; }
    public bool IsAvailableForSelectedDates { get; set; } = true;
    public string AvailabilityNote { get; set; } = string.Empty;
}
