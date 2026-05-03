using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Microsoft.Data.Sqlite;

namespace Bible.Alarm.Shared.Tests;

public sealed class SectionFetcherSqliteExceptionHelperTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void IsBusyOrLocked_ReturnsTrue_WhenSqliteUsesBusyOrLockedCode(int sqliteErrorCode)
    {
        var sqlite = new SqliteException("busy", sqliteErrorCode);
        Assert.True(SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(sqlite));
    }

    [Fact]
    public void IsBusyOrLocked_ReturnsFalse_ForIrrelevantException()
    {
        Assert.False(SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(new InvalidOperationException()));
    }

    [Fact]
    public void IsBusyOrLocked_UnwrapsInnerSqliteExceptions()
    {
        var sqlite = new SqliteException("locked", 6);
        var outer = new Exception("wrapped", sqlite);
        Assert.True(SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(outer));
    }

    [Fact]
    public void IsUniqueConstraintViolation_DetectsErrorCode19()
    {
        var sqlite = new SqliteException("constraint", 19);
        Assert.True(SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(sqlite));

        sqlite = new SqliteException("other", 1);
        Assert.False(SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(sqlite));
    }

    [Fact]
    public void IsUniqueConstraintViolation_UnwrapsInnerSqliteExceptions()
    {
        var sqlite = new SqliteException("constraint", 19);
        var outer = new Exception("wrapped", sqlite);
        Assert.True(SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(outer));
    }

    [Fact]
    public void IsBusyOrLocked_TraversesDeepInnerExceptionChains()
    {
        var sqlite = new SqliteException("busy", 5);

        Assert.True(SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(
            new Exception("layer2", new Exception("layer1", sqlite))));
    }

    [Fact]
    public void IsUniqueConstraintViolation_TraversesDeepInnerExceptionChains()
    {
        var sqlite = new SqliteException("constraint", 19);

        Assert.True(SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(
            new AggregateException(new Exception("inner", sqlite))));
    }

    [Fact]
    public void IsUniqueConstraintViolation_ReturnsFalse_ForIrrelevantExceptionTrees()
    {
        Assert.False(SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(new TimeoutException()));

        var busy = new SqliteException("busy", 5);
        Assert.False(SectionFetcherSqliteExceptionHelper.IsUniqueConstraintViolation(busy));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(19)]
    public void IsBusyOrLocked_ReturnsFalse_WhenSqliteCodeIsNotBusyOrLocked(int sqliteErrorCode)
    {
        var sqlite = new SqliteException("other", sqliteErrorCode);
        Assert.False(SectionFetcherSqliteExceptionHelper.IsBusyOrLocked(sqlite));
    }
}
