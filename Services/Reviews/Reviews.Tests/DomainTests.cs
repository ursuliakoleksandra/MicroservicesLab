using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;
using Xunit;

namespace Reviews.Tests;

/// <summary>Доменні правила перевіряються без жодного звернення до БД.</summary>
public class DomainTests
{
    private static Review NewReview(int rating = 5) => new(42, "Ноутбук X15", new Author(17, "Олена"), rating, "Чудовий екран");

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Review_WithRatingOutOfRange_ThrowsDomainException(int rating)
        => Assert.Throws<DomainException>(() => NewReview(rating));

    [Fact]
    public void Review_NewInstance_HasIdAndVersionZero()
    {
        var review = NewReview();
        Assert.NotEqual(default, review.Id);
        Assert.Equal(0, review.Version);
        Assert.Null(review.UpdatedAt);
    }

    [Fact]
    public void AddComment_AddsEmbeddedComment_AndTouchesDocument()
    {
        var review = NewReview();
        review.AddComment("Андрій", "Згоден");

        Assert.Single(review.Comments);
        Assert.NotNull(review.UpdatedAt);
        Assert.IsNotType<List<Comment>>(review.Comments);      // колекція доступна лише для читання
    }

    [Fact]
    public void AddComment_WithEmptyText_Throws()
        => Assert.Throws<DomainException>(() => NewReview().AddComment("Андрій", "  "));

    [Fact]
    public void ValueObjects_AreComparedByValue()
    {
        Assert.Equal(new Author(1, "Олена"), new Author(1, "Олена"));
        Assert.NotEqual(new Author(1, "Олена"), new Author(2, "Олена"));
        Assert.Equal(new Rating(5), new Rating(5));
        Assert.Equal(new Author(1, "Олена").GetHashCode(), new Author(1, "Олена").GetHashCode());
    }

    [Fact]
    public void Author_WithoutDisplayName_Throws()
        => Assert.Throws<DomainException>(() => new Author(1, " "));

    [Fact]
    public void AttachVideo_ChangesTypeToVideo()
    {
        var review = new Review(3, "Навушники Air", new Author(1, "Олена"), 5);
        review.AttachVideo("https://videos.example.com/a.mp4", 95);
        Assert.Equal(Reviews.Domain.Enums.ReviewType.Video, review.Type);
        Assert.Equal(95, review.DurationSec);
    }
}

public class ArchitectureTests
{
    [Fact]
    public void Domain_HasNoDependenciesOnOtherLayersOrMongoDriver()
    {
        var references = typeof(Review).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();

        Assert.DoesNotContain(references, n => n.StartsWith("Reviews.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.StartsWith("MongoDB.Driver", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        var references = typeof(Reviews.Application.Common.Mappings.ReviewProfile).Assembly
            .GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();

        Assert.Contains("Reviews.Domain", references);
        Assert.DoesNotContain("Reviews.Infrastructure", references);
        Assert.DoesNotContain(references, n => n.StartsWith("MongoDB.Driver", StringComparison.Ordinal));
    }
}
