using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class WindowsVersionPatcherTests
{
    private readonly WindowsVersionPatcher patcher;
    private readonly Mock<IVersionService> versionServiceMock;
    private readonly Mock<IFileService> fileServiceMock;
    private readonly TestPathService testPathService;
    private readonly IFixture fixture;

    public WindowsVersionPatcherTests()
    {
        fixture = new Fixture();
        versionServiceMock = new Mock<IVersionService>();
        fileServiceMock = new Mock<IFileService>();
        testPathService = new TestPathService();
        patcher = new WindowsVersionPatcher(versionServiceMock.Object, fileServiceMock.Object, testPathService);
    }

    [Fact]
    public void PlatformName_ShouldReturnWindows() =>
        // Act & Assert
        patcher.PlatformName.Should().Be("Windows");

    [Fact]
    public async Task PatchVersionAsync_WithValidManifest_ShouldUpdateVersion()
    {
        // Arrange
        var filePath = testPathService.GetWindowsManifestPath();
        var oldVersion = "1.2.0.0";
        var newMajorMinorVersion = "1.3";
        var newVersionName = "1.3.0.0";

        var manifestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package>
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""{oldVersion}"" />
</Package>";

        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);
        versionServiceMock.Setup(x => x.IncrementVersion("1.2")).Returns(newMajorMinorVersion);
        fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion("1.2"), Times.Once);
        fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains(newVersionName))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetWindowsManifestPath();
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidXml_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetWindowsManifestPath();
        var invalidXml = "<invalid-xml>";
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(invalidXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingIdentity_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetWindowsManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><Package></Package>"; // Missing Identity node
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingVersionAttribute_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetWindowsManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" />
</Package>"; // Missing Version attribute
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidVersionFormat_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetWindowsManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""1"" />
</Package>"; // Invalid version format (missing minor)
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
