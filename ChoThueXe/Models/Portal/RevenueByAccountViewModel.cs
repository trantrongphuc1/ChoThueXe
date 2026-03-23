namespace ChoThueXe.Models.Portal;

public class RevenueByAccountViewModel
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public decimal TotalRevenue { get; init; }
}
