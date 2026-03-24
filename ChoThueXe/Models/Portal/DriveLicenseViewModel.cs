namespace ChoThueXe.Models.Portal;

public class DriveLicenseViewModel
{
    public int DriveLicenseId { get; set; }
    public int UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpireAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
