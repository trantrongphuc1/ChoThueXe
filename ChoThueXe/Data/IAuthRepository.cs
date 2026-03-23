using ChoThueXe.Models.Auth;

namespace ChoThueXe.Data;

public interface IAuthRepository
{
    Task<AuthenticatedUserViewModel?> AuthenticateAsync(string email, string password);
    Task<bool> EmailExistsAsync(string email);
    Task RegisterCustomerAsync(RegisterInputModel input);
}
