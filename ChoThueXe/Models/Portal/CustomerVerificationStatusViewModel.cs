namespace ChoThueXe.Models.Portal;

public class CustomerVerificationStatusViewModel
{
    public bool HasCccd { get; init; }
    public string CccdStatus { get; init; } = string.Empty; // PENDING, APPROVED, REJECTED
    public int? CccdDocumentId { get; init; }
    
    public bool HasDriverLicense { get; init; }
    public string DriverLicenseStatus { get; init; } = string.Empty; // PENDING, APPROVED, REJECTED
    public int? DriverLicenseDocumentId { get; init; }
    
    public bool IsVerified => HasCccd && CccdStatus == "APPROVED" && HasDriverLicense && DriverLicenseStatus == "APPROVED";
    public bool IsPending => (HasCccd && CccdStatus == "PENDING") || (HasDriverLicense && DriverLicenseStatus == "PENDING");
    public bool IsRejected => (HasCccd && CccdStatus == "REJECTED") || (HasDriverLicense && DriverLicenseStatus == "REJECTED");
}
