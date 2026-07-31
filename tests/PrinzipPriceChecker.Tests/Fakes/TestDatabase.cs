using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Data;

namespace PrinzipPriceChecker.Tests.Fakes;

internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        Context = CreateContext();
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
