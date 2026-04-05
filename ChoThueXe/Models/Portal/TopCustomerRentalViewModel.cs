namespace ChoThueXe.Models.Portal;

public class TopCustomerRentalViewModel
{
    public int CustomerId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public int RentalCount { get; init; }
    public decimal TotalSpent { get; init; }
}
