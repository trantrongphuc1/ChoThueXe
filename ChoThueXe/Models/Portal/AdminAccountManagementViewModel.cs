namespace ChoThueXe.Models.Portal;

public class AdminAccountManagementViewModel
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public int ContractCount { get; init; }
    public decimal TotalPaid { get; init; }
}
