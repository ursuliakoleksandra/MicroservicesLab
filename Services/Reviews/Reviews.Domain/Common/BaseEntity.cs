using MongoDB.Bson;

namespace Reviews.Domain.Common;

/// <summary>Базова сутність для MongoDB: Id (ObjectId), CreatedAt, UpdatedAt та Version для оптимістичної конкурентності.</summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Збільшує версію; викликає репозиторій безпосередньо перед записом (порівняння версії в фільтрі).</summary>
    public void BumpVersion() => Version++;
}
