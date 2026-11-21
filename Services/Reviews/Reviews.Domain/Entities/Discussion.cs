using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Entities;

/// <summary>
/// Обговорення товару (колекція discussions). Повідомлення (messages) — необмежена підколекція, тому вони
/// винесені в окрему колекцію й зв'язані за посиланням discussionId (referencing), а тут лише лічильник.
/// </summary>
public class Discussion : BaseEntity
{
    private List<string>? _tags;

    public long ProductId { get; private set; }
    public string ProductName { get; private set; } = "";        // денормалізовано
    public string Title { get; private set; } = "";
    public int MessagesCount { get; private set; }
    public DateTime? LastMessageAt { get; private set; }
    public IReadOnlyCollection<string> Tags => (IReadOnlyCollection<string>?)_tags?.AsReadOnly() ?? Array.Empty<string>();

    private Discussion() { }

    public Discussion(long productId, string productName, string title, IEnumerable<string>? tags = null)
    {
        if (productId <= 0) throw new DomainException("ProductId must be positive");
        if (string.IsNullOrWhiteSpace(productName)) throw new DomainException("ProductName is required");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required");
        if (title.Trim().Length > 200) throw new DomainException("Title must be at most 200 characters");

        ProductId = productId;
        ProductName = productName.Trim();
        Title = title.Trim();

        var list = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList();
        if (list is { Count: > 10 }) throw new DomainException("A discussion cannot have more than 10 tags");
        _tags = list is { Count: > 0 } ? list : null;
    }

    public void RegisterMessage()
    {
        MessagesCount++;
        LastMessageAt = DateTime.UtcNow;
        Touch();
    }
}
