#nullable enable

using System.Threading;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryServiceTests
{
    private static async Task<(MediaTestScopeFactory Inner, CountingScopeFactory Counting, SqliteConnection Connection)> CreateFactoryAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var inner = new MediaTestScopeFactory(options);
        return (inner, new CountingScopeFactory(inner), connection);
    }

    private sealed class CountingScopeFactory(MediaTestScopeFactory inner) : IServiceScopeFactory
    {
        private int createScopeCallCount;

        public int CreateScopeCallCount => Volatile.Read(ref createScopeCallCount);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref createScopeCallCount);
            return inner.CreateScope();
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("scope failure");
    }

    private static async Task SeedThreeCategoriesAsync(MediaDbContext db)
    {
        db.Categories.AddRange(
            new Category { CategoryCode = "ZetaCat" },
            new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible },
            new Category { CategoryCode = "AaronCat" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new CategoryService(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CategoryService(new MediaTestScopeFactory(new DbContextOptionsBuilder<MediaDbContext>().Options), null!));
    }

    [Fact]
    public async Task GetAllCategoriesAsync_Orders_Bible_First_Then_CategoryCode_Case_Insensitive()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedThreeCategoriesAsync(seed);
            }

            var sut = new CategoryService(counting, TestLogging.CreateLogger());

            var list = await sut.GetAllCategoriesAsync();

            Assert.Equal(3, list.Count);
            Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, list[0].CategoryCode, StringComparer.Ordinal);
            Assert.Equal("AaronCat", list[1].CategoryCode, StringComparer.Ordinal);
            Assert.Equal("ZetaCat", list[2].CategoryCode, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task GetAllCategoriesAsync_Hits_Database_Once_Then_Uses_Cache()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedThreeCategoriesAsync(seed);
            }

            var sut = new CategoryService(counting, TestLogging.CreateLogger());

            _ = await sut.GetAllCategoriesAsync();
            Assert.Equal(1, counting.CreateScopeCallCount);

            _ = await sut.GetAllCategoriesAsync();
            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetAllCategoriesAsync_Wraps_Scope_Failures_In_InvalidOperationException()
    {
        var sut = new CategoryService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAllCategoriesAsync());

        Assert.Equal("Error getting all categories.", ex.Message);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var (_, counting, connection) = CreateFactorySync();
        using (connection)
        {
            var sut = new CategoryService(counting, TestLogging.CreateLogger());
            sut.Dispose();
            sut.Dispose();
        }
    }

    private static (MediaTestScopeFactory Inner, CountingScopeFactory Counting, SqliteConnection Connection) CreateFactorySync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var init = new MediaDbContext(options))
        {
            init.Database.EnsureCreated();
        }

        var inner = new MediaTestScopeFactory(options);
        return (inner, new CountingScopeFactory(inner), connection);
    }
}
