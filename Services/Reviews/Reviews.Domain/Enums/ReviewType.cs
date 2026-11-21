using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Enums;

/// <summary>Тип відгуку (гнучка схема: різні типи мають різні набори полів).</summary>
public enum ReviewType
{
    Text = 0,
    Video = 1,
    RatingOnly = 2
}

/// <summary>Коди типів, що використовуються в документах і API (збігаються з Лаб. 01): text | video | rating-only.</summary>
public static class ReviewTypeCodes
{
    public static string ToCode(this ReviewType type) => type switch
    {
        ReviewType.Text => "text",
        ReviewType.Video => "video",
        ReviewType.RatingOnly => "rating-only",
        _ => throw new DomainException($"Unknown review type {type}")
    };

    public static ReviewType FromCode(string? code) => code switch
    {
        null or "" or "text" => ReviewType.Text,
        "video" => ReviewType.Video,
        "rating-only" => ReviewType.RatingOnly,
        _ => throw new DomainException($"Unknown review type code '{code}'")
    };
}
