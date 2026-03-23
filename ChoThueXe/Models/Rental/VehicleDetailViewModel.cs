namespace ChoThueXe.Models.Rental;

public class VehicleDetailViewModel
{
    public int VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public decimal PricePerDay { get; set; }
    public string AmenitiesText { get; set; } = string.Empty;
    public string PrimaryImageUrl { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
