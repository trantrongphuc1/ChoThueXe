namespace ChoThueXe.Models.Portal;

public class PendingDocumentViewModel
{
    public int DocumentId { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
