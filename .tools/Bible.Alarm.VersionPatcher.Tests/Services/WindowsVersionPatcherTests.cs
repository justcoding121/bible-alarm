using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class WindowsVersionPatcherTests
{
    private readonly WindowsVersionPatcher _patcher;
    private readonly Mock<IVersionService> _versionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly TestPathService _testPathService;
    private readonly IFixture _fixture;

    public WindowsVersionPatcherTests()
    {
        _fixture = new Fixture();
        _versionServiceMock = new Mock<IVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _testPathService = new TestPathService();
        _patcher = new WindowsVersionPatcher(_versionServiceMock.Object, _fileServiceMock.Object, _testPathService);
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
        var filePath = _testPathService.GetWindowsManifestPath();
        var oldVersion = "1.2.0.0";
        var newMajorMinorVersion = "1.3";
        var newVersionName = "1.3.0.0";

        var manifestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package>
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""{oldVersion}"" />
</Package>";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);
        _versionServiceMock.Setup(x => x.IncrementVersion("1.2")).Returns(newMajorMinorVersion);
        _fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion("1.2"), Times.Once);
        _fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains(newVersionName))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetWindowsManifestPath();
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidXml_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetWindowsManifestPath();
        var invalidXml = "<invalid-xml>";
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
        var filePath = _testPathService.GetWindowsManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><Package></Package>"; // Missing Identity node
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingVersionAttribute_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetWindowsManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" />
</Package>"; // Missing Version attribute
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidVersionFormat_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetWindowsManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""1"" />
</Package>"; // Invalid version format (missing minor)
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}