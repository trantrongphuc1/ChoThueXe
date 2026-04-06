namespace ChoThueXe.Models.Portal;

public class UserVerificationViewModel
{
    public bool CccdVerified { get; init; }
    public bool LicenseVerified { get; init; }

    public bool IsVerified => CccdVerified && LicenseVerified;
}
