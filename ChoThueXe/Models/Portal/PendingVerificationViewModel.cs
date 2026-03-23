namespace ChoThueXe.Models.Portal;

public class PendingVerificationViewModel
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public int? CccdDocumentId { get; init; }
    public string CccdFileUrl { get; init; } = string.Empty;
    public int? DriverLicenseDocumentId { get; init; }
    public string DriverLicenseFileUrl { get; init; } = string.Empty;
}
