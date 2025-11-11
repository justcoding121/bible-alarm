using AutoFixture;
using Bible.Alarm.VersionPatcher.Services.Contracts;
using Bible.Alarm.VersionPatcher.Services.Infrastructure;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests.Services;

public class AndroidVersionPatcherTests
{
    private readonly Mock<IVersionService> _versionServiceMock;
    private readonly Mock<IFileService> _fileServiceMock;
    private readonly TestPathService _testPathService;
    private readonly AndroidVersionPatcher _patcher;
    private readonly IFixture _fixture;

    public AndroidVersionPatcherTests()
    {
        _fixture = new Fixture();
        _versionServiceMock = new Mock<IVersionService>();
        _fileServiceMock = new Mock<IFileService>();
        _testPathService = new TestPathService();
        _patcher = new AndroidVersionPatcher(_versionServiceMock.Object, _fileServiceMock.Object, _testPathService);
    }

    [Fact]
    public void PlatformName_ShouldReturnAndroid()
    {
        // Act & Assert
        _patcher.PlatformName.Should().Be("Android");
    }

    [Fact]
    public async Task PatchVersionAsync_WithValidManifest_ShouldUpdateVersion()
    {
        // Arrange
        var filePath = _testPathService.GetAndroidManifestPath();
        var oldVersionCode = "1";
        var oldVersionName = "1.2";
        var newVersionCode = "2";
        var newVersionName = "1.3";

        var manifestXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android""
    android:versionCode=""{oldVersionCode}""
    android:versionName=""{oldVersionName}"">
</manifest>";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);
        _versionServiceMock.Setup(x => x.IncrementVersionCode(oldVersionCode)).Returns(newVersionCode);
        _versionServiceMock.Setup(x => x.IncrementVersion(oldVersionName)).Returns(newVersionName);
        _fileServiceMock.Setup(x => x.WriteFileAsync(filePath, It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersionCode(oldVersionCode), Times.Once);
        _versionServiceMock.Verify(x => x.IncrementVersion(oldVersionName), Times.Once);
        _fileServiceMock.Verify(x => x.WriteFileAsync(filePath, It.Is<string>(s => s.Contains(newVersionCode) && s.Contains(newVersionName))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetAndroidManifestPath();
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidXml_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetAndroidManifestPath();
        var invalidXml = "<invalid-xml>";

        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(invalidXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingManifestNode_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetAndroidManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><root></root>"; // Missing manifest node
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingVersionAttributes_ShouldNotProcess()
    {
        // Arrange
        var filePath = _testPathService.GetAndroidManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><manifest></manifest>"; // Missing version attributes
        _fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        _fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        // Act
        await _patcher.PatchVersionAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        _versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        _fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}