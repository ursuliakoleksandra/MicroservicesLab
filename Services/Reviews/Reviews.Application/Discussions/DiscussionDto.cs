namespace Reviews.Application.Discussions;

public class DiscussionDto
{
    public string Id { get; set; } = "";
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public int MessagesCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Version { get; set; }
}
