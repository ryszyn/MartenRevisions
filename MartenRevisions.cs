namespace MartenRevisions;

using System.Runtime.Serialization;
using Marten;
using Marten.Schema;

public sealed class DbEntity
{
    public string? AnyText { get; set; }
    public Guid Id { get; set; }

    [IgnoreDataMember, Version]
    public int Version { get; set; }

    public DbEntity(Guid id, string? someText, int version = 1)
    {
        this.Id = id;
        this.AnyText = someText;
        this.Version = version;
    }
}

public sealed class MartenDbOptions
{
    public string ConnectionString = "Server=localhost;Port=5432;Database=martendocrevisions;User Id=postgres;Password=postgres;";
}

internal class MartenSampleRepository
{
    private readonly DocumentStore store;

    public MartenSampleRepository(MartenDbOptions options)
    {
        this.store = DocumentStore.For(config =>
        {
            config.Connection(options.ConnectionString);
            config.AutoCreateSchemaObjects = Weasel.Core.AutoCreate.All;
        });
    }

    public async Task AddAsync(DbEntity dbEntity, CancellationToken cancellationToken = default)
    {
        await using var session = this.store.LightweightSession();
        session.Insert(dbEntity);
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<DbEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var session = this.store.QuerySession();
        var dbEntity = await session.LoadAsync<DbEntity>(id, cancellationToken);

        return dbEntity;
    }

    public async Task UpdateAsync(DbEntity dbEntity, CancellationToken cancellationToken = default)
    {
        var revision = dbEntity.Version + 1; //increment version before update...

        try
        {
            await using var session = this.store.LightweightSession();
            session.UpdateRevision(dbEntity, revision);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            var id = dbEntity.Id.ToString();
            var message = $"The record with Id {id} has already been updated by another user.";

            throw new Exception(message);
        }
    }

    public async Task UpdateWithStoreAsync(DbEntity dbEntity, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = this.store.LightweightSession();
            session.Store(dbEntity);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            var id = dbEntity.Id.ToString();
            var message = $"The record with Id {id} has already been updated by another user.";

            throw new Exception(message);
        }
    }
}
