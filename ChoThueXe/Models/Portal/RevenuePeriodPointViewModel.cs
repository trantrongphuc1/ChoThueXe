namespace ChoThueXe.Models.Portal;

public class RevenuePeriodPointViewModel
{
    public string PeriodCode { get; init; } = string.Empty;
    public string PeriodLabel { get; init; } = string.Empty;
    public decimal TotalRevenue { get; init; }
    public int ContractCount { get; init; }
}
