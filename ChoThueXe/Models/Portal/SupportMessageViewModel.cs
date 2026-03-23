namespace ChoThueXe.Models.Portal;

public class SupportMessageViewModel
{
    public int MessageId { get; init; }
    public int SenderId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public int ReceiverId { get; init; }
    public string ReceiverName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ReplyContent { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public DateTime? RepliedAt { get; init; }
}
