using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class VersionServiceTests
{
    private readonly VersionService versionService = new();
    private readonly IFixture fixture = new Fixture();

    [Theory]
    [InlineData("1.2", "1.3")]
    [InlineData("1.99", "2.0")]
    [InlineData("5.10", "5.11")]
    [InlineData("10.99", "11.0")]
    public void IncrementVersion_ShouldIncrementCorrectly(string input, string expected)
    {
        // Act
        var result = versionService.IncrementVersion(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", "2")]
    [InlineData("99", "100")]
    [InlineData("1000", "1001")]
    public void IncrementVersionCode_ShouldIncrementCorrectly(string input, string expected)
    {
        // Act
        var result = versionService.IncrementVersionCode(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IncrementVersion_WithInvalidFormat_ShouldThrowArgumentException()
    {
        // Arrange
        const string InvalidVersion = "invalid";

        // Act & Assert
        var action = () => versionService.IncrementVersion(InvalidVersion);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IncrementVersionCode_WithInvalidFormat_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidVersionCode = "invalid";

        // Act & Assert
        var action = () => versionService.IncrementVersionCode(invalidVersionCode);
        action.Should().Throw<ArgumentException>();
    }
}
