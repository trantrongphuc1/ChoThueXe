namespace ChoThueXe.Models.Portal;

public class ReviewableContractViewModel
{
    public int ContractId { get; init; }
    public int VehicleId { get; init; }
    public string VehicleName { get; init; } = string.Empty;
    public DateTime EndDate { get; init; }
}
