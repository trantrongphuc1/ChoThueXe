namespace ChoThueXe.Models.Portal;

public class SubmitDriveLicenseInputModel
{
    public string LicenseNumber { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpireAt { get; set; }
    public string FileUrl { get; set; } = string.Empty;
}
