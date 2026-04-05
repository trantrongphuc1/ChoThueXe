namespace ChoThueXe.Models.Portal;

public class CustomerVerificationStatusViewModel
{
    public bool HasCccd { get; init; }
    public string CccdStatus { get; init; } = string.Empty; // PENDING, APPROVED, REJECTED
    public int? CccdDocumentId { get; init; }
    
    public bool HasDriverLicense { get; init; }
    public string DriverLicenseStatus { get; init; } = string.Empty; // PENDING, APPROVED, REJECTED
    public int? DriverLicenseDocumentId { get; init; }
    
    public bool IsVerified =>
        HasCccd
        && string.Equals(CccdStatus?.Trim(), "APPROVED", StringComparison.OrdinalIgnoreCase)
        && HasDriverLicense
        && string.Equals(DriverLicenseStatus?.Trim(), "APPROVED", StringComparison.OrdinalIgnoreCase);

    public bool IsPending =>
        (HasCccd && string.Equals(CccdStatus?.Trim(), "PENDING", StringComparison.OrdinalIgnoreCase))
        || (HasDriverLicense && string.Equals(DriverLicenseStatus?.Trim(), "PENDING", StringComparison.OrdinalIgnoreCase));

    public bool IsRejected =>
        (HasCccd && string.Equals(CccdStatus?.Trim(), "REJECTED", StringComparison.OrdinalIgnoreCase))
        || (HasDriverLicense && string.Equals(DriverLicenseStatus?.Trim(), "REJECTED", StringComparison.OrdinalIgnoreCase));
}
