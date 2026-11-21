using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Enums;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

/// <summary>
/// Агрегат «Відгук» (документ колекції reviews). Приватні setter-и, валідація в конструкторі й методах,
/// вкладені Value Objects (author, rating, verifiedPurchase) та вкладений масив comments.
/// Необов'язкові поля (photos, pros, cons, videoUrl...) відсутні в документі, якщо не задані — гнучка схема NoSQL.
/// </summary>
public class Review : BaseEntity
{
    private const int MaxTextLength = 2000;
    private const int MaxListItems = 10;
    private const int MaxComments = 500;

    // приватні поля — їх мапить BsonClassMap в Infrastructure (атрибутів у домені немає)
    private List<Comment> _comments = new();
    private List<string>? _photos;
    private List<string>? _pros;
    private List<string>? _cons;

    public long ProductId { get; private set; }                  // посилання на CatalogDb (без FK)
    public string ProductName { get; private set; } = "";        // денормалізовано (політика дублювання)
    public Author Author { get; private set; } = null!;          // Value Object
    public Rating Rating { get; private set; } = null!;          // Value Object
    public ReviewType Type { get; private set; }
    public string? Text { get; private set; }
    public string? VideoUrl { get; private set; }
    public int? DurationSec { get; private set; }
    public VerifiedPurchase? VerifiedPurchase { get; private set; }
    public ObjectId? DiscussionId { get; private set; }          // reference на колекцію discussions

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<string> Photos => (IReadOnlyCollection<string>?)_photos?.AsReadOnly() ?? Array.Empty<string>();
    public IReadOnlyCollection<string> Pros => (IReadOnlyCollection<string>?)_pros?.AsReadOnly() ?? Array.Empty<string>();
    public IReadOnlyCollection<string> Cons => (IReadOnlyCollection<string>?)_cons?.AsReadOnly() ?? Array.Empty<string>();

    private Review() { }                                          // для десеріалізації

    public Review(long productId, string productName, Author author, int rating, string? text = null)
    {
        if (productId <= 0) throw new DomainException("ProductId must be positive");
        if (string.IsNullOrWhiteSpace(productName)) throw new DomainException("ProductName is required");
        if (productName.Trim().Length > 200) throw new DomainException("ProductName must be at most 200 characters");

        ProductId = productId;
        ProductName = productName.Trim();
        Author = author ?? throw new DomainException("Author is required");
        Rating = new Rating(rating);                              // 10 -> DomainException без звернення до БД
        Text = NormalizeText(text);
        Type = Text is null ? ReviewType.RatingOnly : ReviewType.Text;
    }

    public void Edit(int rating, string? text)
    {
        Rating = new Rating(rating);
        Text = NormalizeText(text);
        if (Type != ReviewType.Video) Type = Text is null ? ReviewType.RatingOnly : ReviewType.Text;
        Touch();
    }

    public void AddComment(string authorName, string text)
    {
        if (_comments.Count >= MaxComments) throw new DomainException($"A review cannot have more than {MaxComments} comments");
        _comments.Add(new Comment(authorName, text, DateTime.UtcNow));   // вкладений документ
        Touch();
    }

    public void AddPhotos(IEnumerable<string> urls)
    {
        var list = _photos ??= new List<string>();
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > 500) throw new DomainException("Photo url is invalid");
            if (list.Count >= MaxListItems) throw new DomainException($"A review cannot have more than {MaxListItems} photos");
            list.Add(url.Trim());
        }
        if (list.Count == 0) _photos = null;
        Touch();
    }

    public void SetProsAndCons(IEnumerable<string>? pros, IEnumerable<string>? cons)
    {
        _pros = NormalizeList(pros, "Pro");
        _cons = NormalizeList(cons, "Con");
        Touch();
    }

    public void AttachVideo(string url, int durationSec)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500) throw new DomainException("Video url is invalid");
        if (durationSec <= 0) throw new DomainException("Video duration must be positive");
        VideoUrl = url.Trim();
        DurationSec = durationSec;
        Type = ReviewType.Video;
        Touch();
    }

    public void MarkVerifiedPurchase(long orderId)
    {
        VerifiedPurchase = new VerifiedPurchase(orderId);
        Touch();
    }

    public void LinkDiscussion(ObjectId discussionId)
    {
        DiscussionId = discussionId;
        Touch();
    }

    private static string? NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength) throw new DomainException($"Text must be at most {MaxTextLength} characters");
        return trimmed;
    }

    private static List<string>? NormalizeList(IEnumerable<string>? items, string name)
    {
        if (items is null) return null;
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();
        if (list.Count > MaxListItems) throw new DomainException($"A review cannot have more than {MaxListItems} items in '{name}' list");
        if (list.Any(i => i.Length > 200)) throw new DomainException($"{name} text must be at most 200 characters");
        return list.Count == 0 ? null : list;
    }
}
