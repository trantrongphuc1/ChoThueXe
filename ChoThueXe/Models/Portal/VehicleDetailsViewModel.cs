using ChoThueXe.Models.Rental;

namespace ChoThueXe.Models.Portal;

public class VehicleDetailsViewModel
{
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public decimal PricePerDay { get; set; }
    public string AmenitiesText { get; set; } = string.Empty;
    public string PrimaryImageUrl { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public IReadOnlyList<RentalDateRange> RentalDates { get; set; } = [];
    public DateTime? SelectedCheckInDate { get; set; }
    public DateTime? SelectedCheckOutDate { get; set; }
    public bool? IsAvailableForSelectedDates { get; set; }
    public string AvailabilityNote { get; set; } = string.Empty;
    public decimal? EstimatedRentalCost { get; set; }
    public int? EstimatedRentalDays { get; set; }
}

public class RentalDateRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}