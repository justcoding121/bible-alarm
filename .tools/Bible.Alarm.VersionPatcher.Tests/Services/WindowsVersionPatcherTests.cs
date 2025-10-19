using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;
using System.IO;
using Xunit;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class WindowsVersionPatcherTests
{
    private readonly Mock<IVersionService> _versionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly WindowsVersionPatcher _patcher;
    private readonly IFixture _fixture;

    public WindowsVersionPatcherTests()
    {
        _fixture = new Fixture();
        _versionServiceMock = new Mock<IVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _patcher = new WindowsVersionPatcher(_versionServiceMock.Object, _fileServiceMock.Object);
    }

    [Fact]
    public void PlatformName_ShouldReturnWindows()
    {
        // Act & Assert
        _patcher.PlatformName.Should().Be("Windows");
    }

    [Fact]
    public async Task PatchVersionAsync_WithValidManifest_ShouldUpdateVersion()
    {
        // Arrange
        var filePath = Path.Combine("src", "Bible.Alarm", "Platforms", "Windows", "Package.appxmanifest");
        var oldVersion = "1.2.0.0";
        var newVersion = "1.3.0.0";

        var manifestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""{oldVersion}"" />
</Package>";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);
        _versionServiceMock.Setup(x => x.IncrementVersion("1.2")).Returns("1.3");
        _fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion("1.2"), Times.Once);
        _fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains("1.3.0.0"))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = "non_existent.xml";
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidXml_ShouldNotProcess()
    {
        // Arrange
        var filePath = "invalid.xml";
        var invalidXml = "invalid xml content";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(invalidXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingIdentity_ShouldNotProcess()
    {
        // Arrange
        var filePath = "test_manifest.xml";
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Properties>
        <DisplayName>Test App</DisplayName>
    </Properties>
</Package>";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
