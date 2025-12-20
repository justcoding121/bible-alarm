using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class IosVersionPatcherTests
{
    private readonly Mock<IVersionService> versionServiceMock;
    private readonly Mock<IFileService> fileServiceMock;
    private readonly TestPathService testPathService;
    private readonly IosVersionPatcher patcher;
    private readonly IFixture fixture;

    public IosVersionPatcherTests()
    {
        fixture = new Fixture();
        versionServiceMock = new Mock<IVersionService>();
        fileServiceMock = new Mock<IFileService>();
        testPathService = new TestPathService();
        patcher = new IosVersionPatcher(versionServiceMock.Object, fileServiceMock.Object, testPathService);
    }

    [Fact]
    public void PlatformName_ShouldReturnIOS()
    {
        // Act & Assert
        patcher.PlatformName.Should().Be("iOS");
    }

    [Fact]
    public async Task PatchVersionAsync_WithValidPlist_ShouldUpdateVersion()
    {
        // Arrange
        var filePath = testPathService.GetIosInfoPlistPath();
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

        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(plistContent);
        versionServiceMock.Setup(x => x.IncrementVersion(oldVersion)).Returns(newVersion);
        fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion(oldVersion), Times.Once);
        fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains(newVersion))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetIosInfoPlistPath();
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingCFBundleVersion_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetIosInfoPlistPath();
        var plistContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>SomeOtherKey</key>
    <string>SomeValue</string>
</dict>
</plist>";
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(plistContent);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
