namespace ChoThueXe.Models.Portal;

public class PendingProfileUpdateRequestViewModel
{
    public int RequestId { get; init; }
    public int UserId { get; init; }
    public string CurrentFullName { get; init; } = string.Empty;
    public string CurrentPhone { get; init; } = string.Empty;
    public string RequestedFullName { get; init; } = string.Empty;
    public string RequestedPhone { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}
