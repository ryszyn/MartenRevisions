namespace MartenRevisions;

using FluentAssertions;
using Marten;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Respawn;

[TestClass]
public class MartenRevisonsTestsNoWrapper
{
    private const string CONNECTION_STRING = "Server=localhost;Port=5432;Database=martendocrevisions;User Id=postgres;Password=postgres;";
    private readonly CancellationToken cancellationToken = CancellationToken.None;
    private readonly NpgsqlConnection connection = new(CONNECTION_STRING);
    private readonly MartenDbOptions options;
    private readonly MartenSampleRepository repository;
    private Respawner? respawner;
    private DocumentStore store;

    public MartenRevisonsTestsNoWrapper()
    {
        this.options = new MartenDbOptions
        {
            ConnectionString = CONNECTION_STRING,
        };

        this.repository = new MartenSampleRepository(this.options);

        this.store = DocumentStore.For(config =>
        {
            config.Connection(options.ConnectionString);
            config.AutoCreateSchemaObjects = Weasel.Core.AutoCreate.All;
        });
    }


    [TestMethod]
    public async Task Store_Should_Update_Record_When_Version_In_Database_Equal()
    {
        // Arrange
        var id = Guid.NewGuid();
        var anyText = "Any text";
        var entity = new DbEntity(id, anyText);


        //user_1, version 1
        await using var session = this.store.LightweightSession();
        session.Insert(entity);
        await session.SaveChangesAsync(cancellationToken);

        //user_2, version 1 -> 2
        await using var session2 = this.store.LightweightSession();
        var dbEntity = await session.LoadAsync<DbEntity>(id, cancellationToken);

        dbEntity!.AnyText = anyText + " updated";

        session2.Store(dbEntity);

        // Act
        await session2.SaveChangesAsync(cancellationToken);

        // Assert
        var readEntity = await session2.LoadAsync<DbEntity>(id, cancellationToken);

        readEntity.Should()
            .NotBeNull()
            ;

        readEntity!.Id.Should()
            .Be(id)
            ;

        readEntity.AnyText.Should()
            .Be(dbEntity.AnyText)
            ;

        readEntity.Version.Should()
            .Be(dbEntity.Version)
            ;
    }


    [TestMethod]
    public async Task Store_Should_Throw_Exception_When_Version_In_Database_Higher()
    {
        // Arrange
        var id = Guid.NewGuid();
        var anyText = "Any text";
        var entity = new DbEntity(id, anyText);


        //user_1, version 1
        await using var session = this.store.LightweightSession();
        session.Insert(entity);
        await session.SaveChangesAsync(cancellationToken);

        var outdatedVersion = entity.Version; //for user 3

        //user_2, version 1 -> 2
        await using var session2 = this.store.LightweightSession();
        var dbEntity = await session.LoadAsync<DbEntity>(id, cancellationToken);

        dbEntity!.AnyText = anyText + " updated";

        session2.Store(dbEntity);
        await session2.SaveChangesAsync(cancellationToken);

        //user_3, document read before user_2 saved their document, should throw exception...
        await using var session3 = this.store.LightweightSession();
        var outdatedText = anyText + " update attempt too late...";
        var outdatedEntity = new DbEntity(id, outdatedText, outdatedVersion);

        session3.Store<DbEntity>(outdatedEntity);

        // Act
        var action = async () => await session3.SaveChangesAsync(cancellationToken);

        // Assert
        await action.Should()
            .ThrowAsync<Exception>()
            ;
    }


    [TestCleanup]
    public async Task Teardown()
    {
        await this.respawner!.ResetAsync(this.connection);
        await this.connection.CloseAsync();
    }

    [TestInitialize]
    public async Task TestInit()
    {
        await this.connection.OpenAsync(this.cancellationToken);

        var respawnerOptions = new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
        };

        this.respawner = await Respawner.CreateAsync(this.connection, respawnerOptions);
    }
}
