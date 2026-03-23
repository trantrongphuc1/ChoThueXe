namespace ChoThueXe.Models.Rental;

public class PendingContractViewModel
{
    public int ContractId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = string.Empty;

    public decimal DueAmount => TotalAmount - PaidAmount;
}
