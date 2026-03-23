using System.Security.Claims;

namespace ChoThueXe.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("Invalid user id claim.");
        }

        return userId;
    }
}
