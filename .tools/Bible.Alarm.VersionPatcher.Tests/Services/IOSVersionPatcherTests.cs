using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class IOSVersionPatcherTests
{
    private readonly Mock<IVersionService> _versionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly TestPathService _testPathService;
    private readonly IOSVersionPatcher _patcher;
    private readonly IFixture _fixture;

    public IOSVersionPatcherTests()
    {
        _fixture = new Fixture();
        _versionServiceMock = new Mock<IVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _testPathService = new TestPathService();
        _patcher = new IOSVersionPatcher(_versionServiceMock.Object, _fileServiceMock.Object, _testPathService);
    }

    [Fact]
    public void PlatformName_ShouldReturnIOS()
    {
        // Act & Assert
        _patcher.PlatformName.Should().Be("iOS");
    }

    [Fact]
    public async Task PatchVersionAsync_WithValidPlist_ShouldUpdateVersion()
    {
        // Arrange
        var filePath = _testPathService.GetIOSInfoPlistPath();
        var oldVersion = "1.2";
        var newVersion = "1.3";

        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleVersion</key>
    <string>{oldVersion}</string>
</dict>
</plist>";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(plistContent);
        _versionServiceMock.Setup(x => x.IncrementVersion(oldVersion)).Returns(newVersion);
        _fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(oldVersion), Times.Once);
        _fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains(newVersion))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetIOSInfoPlistPath();
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingCFBundleVersion_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetIOSInfoPlistPath();
        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>SomeOtherKey</key>
    <string>SomeValue</string>
</dict>
</plist>";
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(plistContent);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}