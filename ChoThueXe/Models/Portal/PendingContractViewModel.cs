namespace ChoThueXe.Models.Portal;

public class PendingContractViewModel
{
    public int ContractId { get; init; }
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    
    public string Status { get; init; } = string.Empty;

    public decimal DueAmount => TotalAmount - PaidAmount;
}
