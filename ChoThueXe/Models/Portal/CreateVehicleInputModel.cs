using System.ComponentModel.DataAnnotations;

namespace ChoThueXe.Models.Portal;

public class CreateVehicleInputModel
{
    [Range(0, int.MaxValue)]
    public int VehicleId { get; set; }

    [Range(1, int.MaxValue)]
    public int OwnerId { get; set; }

    [Range(1, int.MaxValue)]
    public int BrandId { get; set; }

    [Range(1, int.MaxValue)]
    public int TypeId { get; set; }

    [Required]
    public string VehicleName { get; set; } = string.Empty;

    [Required]
    public string LicensePlate { get; set; } = string.Empty;

    [Range(2, 60)]
    public int Seats { get; set; }

    [Required]
    public string Transmission { get; set; } = "Auto";

    [Required]
    public string FuelType { get; set; } = "Gas";

    [Range(typeof(decimal), "1", "999999999")]
    public decimal PricePerDay { get; set; }

    [Required]
    public string Status { get; set; } = "AVAILABLE";

    // Accept multiple image URLs separated by newline, comma, or semicolon.
    public string? ImageUrls { get; set; }

    public List<string> SelectedAmenities { get; set; } = [];
}
