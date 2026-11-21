namespace Reviews.Application.Reviews;

/// <summary>DTO відгуку. У списках Comments = null (лише CommentsCount), у GetById/після змін — повний масив коментарів.</summary>
public class ReviewDto
{
    public string Id { get; set; } = "";
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public long AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public string Type { get; set; } = "text";              // text | video | rating-only
    public int Rating { get; set; }
    public string? Text { get; set; }
    public List<string> Photos { get; set; } = new();
    public List<string> Pros { get; set; } = new();
    public List<string> Cons { get; set; } = new();
    public string? VideoUrl { get; set; }
    public int? DurationSec { get; set; }
    public long? OrderId { get; set; }                      // verifiedPurchase.orderId
    public string? DiscussionId { get; set; }
    public int CommentsCount { get; set; }
    public List<CommentDto>? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; }
}

public class CommentDto
{
    public string AuthorName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
