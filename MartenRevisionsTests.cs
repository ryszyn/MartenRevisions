namespace MartenRevisions;

using FluentAssertions;
using Marten;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Respawn;

[TestClass]
public class MartenDbTests
{
    private const string CONNECTION_STRING = "Server=localhost;Port=5432;Database=martendocrevisions;User Id=postgres;Password=postgres;";
    private readonly CancellationToken cancellationToken = CancellationToken.None;
    private readonly NpgsqlConnection connection = new(CONNECTION_STRING);
    private readonly MartenDbOptions options;
    private readonly MartenSampleRepository repository;
    private Respawner? respawner;
    private DocumentStore store;

    public MartenDbTests()
    {
        this.options = new MartenDbOptions
        {
            ConnectionString = CONNECTION_STRING,
        };

        this.repository = new MartenSampleRepository(this.options);
    }

    [TestMethod]
    public async Task AddAsync_Should_Add_New_Entity()
    {
        // Arrange
        var id = Guid.NewGuid();
        var anyText = "Any text";
        var entity = new DbEntity(id, anyText);

        // Act
        await this.repository.AddAsync(entity, this.cancellationToken);

        // Assert
        var addedEntity = await this.repository.GetAsync(id, this.cancellationToken);

        addedEntity.Should().NotBeNull();
        addedEntity!.Id.Should().Be(id);
        addedEntity.AnyText.Should().Be(anyText);
        addedEntity.Version.Should().Be(1);
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

    [TestMethod]
    public async Task UpdateAsync_Should_Throw_Exception_When_Version_In_Database_Equal()
    {
        // Arrange
        var id = Guid.NewGuid();
        var anyText = "Any text";
        var version = 3; //any>1 let's say 3...

        //add entity with version... ok, it works
        var entity = new DbEntity(id, anyText, version);
        await this.repository.AddAsync(entity, this.cancellationToken);

        //check only - get by id
        var savedEntity = await this.repository.GetAsync(id, this.cancellationToken);

        //let's simulate update attempt with outdated version - assuming someone has already changed it to 3, but we are still working with version 2...
        var updatedText = savedEntity!.AnyText + " - updated";
        var outdatedVersion = savedEntity.Version - 1; // Simulate outdated version

        var updatedEntity = new DbEntity(savedEntity.Id, updatedText, outdatedVersion);

        // Act

        //update method will try to increment version by 1 - new value will be 3...
        // ...and according to documentation - UpdateRevision (entity, 3) should throw exception,
        // because someone already updated the document:
        // // https://martendb.io/documents/concurrency
        // // *This* operation will enforce the optimistic concurrency
        // // The supplied revision number should be the *new* revision number,
        // // but will be rejected with a ConcurrencyException when SaveChanges() is
        // // called if the version
        // // in the database is equal or greater than the supplied revision

        var action = async () => await this.repository.UpdateAsync(updatedEntity, this.cancellationToken);
        //no exception thrown, no update performed, nothing happened (?)

        // Assert
        var expectedMessage = $"The record with Id {id} has already been updated by another user.";

        await action.Should()
            .ThrowAsync<Exception>()
            .WithMessage(expectedMessage);
    }

    [TestMethod]
    public async Task UpdateAsync_Should_Throw_Exception_When_Version_In_Database_Greater()
    {
        // Arrange
        var id = Guid.NewGuid();
        var anyText = "Any text";
        var version = 3; //any>1 let's say 5...

        var entity = new DbEntity(id, anyText, version);
        await this.repository.AddAsync(entity, this.cancellationToken);

        //check only - get by id
        var savedEntity = await this.repository.GetAsync(id, this.cancellationToken);

        var updatedText = savedEntity!.AnyText + " - updated";
        var outdatedVersion = savedEntity.Version - 2; // Simulate outdated version with version in db lower

        var updatedEntity = new DbEntity(savedEntity.Id, updatedText, outdatedVersion);

        // Act

        var action = async () => await this.repository.UpdateAsync(updatedEntity, this.cancellationToken);

        // Assert
        var expectedMessage = $"The record with Id {id} has already been updated by another user.";

        await action.Should()
            .ThrowAsync<Exception>()
            .WithMessage(expectedMessage);
    }

    [TestMethod]
    public async Task UpdateAsync_Should_Update_Record_When_Version_In_Database_Lower()
    {
        // Arrange
        var id = Guid.NewGuid();
        var anyText = "Any text";
        var entity = new DbEntity(id, anyText, version: 1);

        await this.repository.AddAsync(entity, this.cancellationToken);

        var savedEntity = await this.repository.GetAsync(id, this.cancellationToken);

        var updatedText = savedEntity!.AnyText + " - updated";
        var updatedEntity = new DbEntity(savedEntity.Id, updatedText, savedEntity.Version);

        // Act
        await this.repository.UpdateAsync(updatedEntity, this.cancellationToken);

        // Assert
        var readEntity = await this.repository.GetAsync(id, this.cancellationToken);

        readEntity.Should()
            .NotBeNull()
            ;

        readEntity!.Id.Should()
            .Be(id)
            ;

        readEntity.AnyText.Should()
            .Be(updatedText)
            ;

        readEntity.Version.Should()
            .BeGreaterThan(savedEntity.Version)
            ;
    }
}
