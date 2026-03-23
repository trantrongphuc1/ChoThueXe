namespace ChoThueXe.Models.Rental;

public class ContractFullViewModel
{
    public int ContractId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}
