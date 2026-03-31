using ChoThueXe.Models.Auth;

namespace ChoThueXe.Data;

public interface IAuthRepository
{
    Task<AuthenticatedUserViewModel?> AuthenticateAsync(string email, string password);
    Task<bool> EmailExistsAsync(string email);
    Task RegisterCustomerAsync(RegisterInputModel input);
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword);

    Task<string> GenerateOtpAsync(string email);
    Task<bool> ValidateOtpAsync(string email, string otpCode);
    Task ResetPasswordAsync(string email, string newPassword);
}
