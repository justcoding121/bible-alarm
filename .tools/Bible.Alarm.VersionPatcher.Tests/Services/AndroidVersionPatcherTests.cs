using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class AndroidVersionPatcherTests
{
    private readonly Mock<IVersionService> versionServiceMock;
    private readonly Mock<IFileService> fileServiceMock;
    private readonly TestPathService testPathService;
    private readonly AndroidVersionPatcher patcher;
    private readonly IFixture fixture;

    public AndroidVersionPatcherTests()
    {
        fixture = new Fixture();
        versionServiceMock = new Mock<IVersionService>();
        fileServiceMock = new Mock<IFileService>();
        testPathService = new TestPathService();
        patcher = new AndroidVersionPatcher(versionServiceMock.Object, fileServiceMock.Object, testPathService);
    }

    [Fact]
    public void PlatformName_ShouldReturnAndroid()
    {
        // Act & Assert
        patcher.PlatformName.Should().Be("Android");
    }

    [Fact]
    public async Task PatchVersionAsync_WithValidManifest_ShouldUpdateVersion()
    {
        // Arrange
        var filePath = testPathService.GetAndroidManifestPath();
        var oldVersionCode = "1";
        var oldVersionName = "1.2";
        var newVersionCode = "2";
        var newVersionName = "1.3";

        var manifestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android""
    android:versionCode=""{oldVersionCode}""
    android:versionName=""{oldVersionName}"">
</manifest>";

        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);
        versionServiceMock.Setup(x => x.IncrementVersionCode(oldVersionCode)).Returns(newVersionCode);
        versionServiceMock.Setup(x => x.IncrementVersion(oldVersionName)).Returns(newVersionName);
        fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersionCode(oldVersionCode), Times.Once);
        versionServiceMock.Verify(x => x.IncrementVersion(oldVersionName), Times.Once);
        fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains(newVersionCode) && s.Contains(newVersionName))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetAndroidManifestPath();
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidXml_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetAndroidManifestPath();
        var invalidXml = "<invalid-xml>";

        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(invalidXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingManifestNode_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetAndroidManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><root></root>"; // Missing manifest node
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingVersionAttributes_ShouldNotProcess()
    {
        // Arrange
        var filePath = testPathService.GetAndroidManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><manifest></manifest>"; // Missing version attributes
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await patcher.PatchVersionAsync();

        // Assert
        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
