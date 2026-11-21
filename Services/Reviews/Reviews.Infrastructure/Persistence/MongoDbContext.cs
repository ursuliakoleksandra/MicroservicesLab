using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Надає IMongoCollection&lt;T&gt; для всіх доменних сутностей. Singleton: MongoClient потокобезпечний і має власний пул з'єднань.</summary>
public class MongoDbContext
{
    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is not configured");

        // Лаб. 05: кожна команда MongoDB стає span у trace (поруч зі spans HTTP та SQL)
        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        clientSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        Client = new MongoClient(clientSettings);
        Database = Client.GetDatabase(settings.DatabaseName);
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>("reviews");
    public IMongoCollection<Discussion> Discussions => Database.GetCollection<Discussion>("discussions");
}
