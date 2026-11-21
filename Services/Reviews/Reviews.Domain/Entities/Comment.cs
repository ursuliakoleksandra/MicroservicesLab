using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Entities;

/// <summary>Коментар до відгуку — незмінний вкладений документ (embedding): живе й видаляється разом із відгуком.</summary>
public sealed class Comment
{
    public string AuthorName { get; }
    public string Text { get; }
    public DateTime CreatedAt { get; }

    public Comment(string authorName, string text, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(authorName)) throw new DomainException("Comment author is required");
        if (string.IsNullOrWhiteSpace(text)) throw new DomainException("Comment text is required");
        if (text.Trim().Length > 1000) throw new DomainException("Comment text must be at most 1000 characters");

        AuthorName = authorName.Trim();
        Text = text.Trim();
        CreatedAt = createdAt;
    }
}
