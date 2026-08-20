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
    public void PlatformName_ShouldReturnAndroid() =>
        patcher.PlatformName.Should().Be("Android");

    [Fact]
    public async Task PatchVersionAsync_WithValidManifest_ShouldUpdateVersion()
    {
        // Android patcher uses csproj (ApplicationVersion / ApplicationDisplayVersion), not AndroidManifest.xml
        var csprojPath = testPathService.GetCsprojPath();
        var oldVersionCode = "1";
        var oldVersionName = "1.2";
        var newVersionCode = "2";
        var newVersionName = "1.3";

        var csprojXml = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <ApplicationVersion>{oldVersionCode}</ApplicationVersion>
    <ApplicationDisplayVersion>{oldVersionName}</ApplicationDisplayVersion>
  </PropertyGroup>
</Project>";

        fileServiceMock.Setup(x => x.FileExists(csprojPath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(csprojPath)).ReturnsAsync(csprojXml);
        versionServiceMock.Setup(x => x.IncrementVersionCode(oldVersionCode)).Returns(newVersionCode);
        versionServiceMock.Setup(x => x.IncrementVersion(oldVersionName)).Returns(newVersionName);
        fileServiceMock.Setup(x => x.WriteFileAsync(csprojPath, It.IsAny<string>())).Returns(Task.CompletedTask);

        await patcher.PatchVersionAsync();

        versionServiceMock.Verify(x => x.IncrementVersionCode(oldVersionCode), Times.Once);
        versionServiceMock.Verify(x => x.IncrementVersion(oldVersionName), Times.Once);
        fileServiceMock.Verify(x => x.WriteFileAsync(csprojPath, It.Is<string>(s => s.Contains(newVersionCode) && s.Contains(newVersionName))), Times.Once);
    }

    [Fact]
    public async Task PatchVersionAsync_WithNonExistentFile_ShouldNotProcess()
    {
        var filePath = testPathService.GetAndroidManifestPath();
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(false);

        await patcher.PatchVersionAsync();

        fileServiceMock.Verify(x => x.ReadFileAsync(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithInvalidXml_ShouldNotProcess()
    {
        var filePath = testPathService.GetAndroidManifestPath();
        var invalidXml = "<invalid-xml>";

        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(invalidXml);

        await patcher.PatchVersionAsync();

        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingManifestNode_ShouldNotProcess()
    {
        var filePath = testPathService.GetAndroidManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><root></root>";
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        await patcher.PatchVersionAsync();

        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PatchVersionAsync_WithMissingVersionAttributes_ShouldNotProcess()
    {
        var filePath = testPathService.GetAndroidManifestPath();
        var manifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?><manifest></manifest>";
        fileServiceMock.Setup(x => x.FileExists(filePath)).Returns(true);
        fileServiceMock.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(manifestXml);

        await patcher.PatchVersionAsync();

        versionServiceMock.Verify(x => x.IncrementVersionCode(It.IsAny<string>()), Times.Never);
        versionServiceMock.Verify(x => x.IncrementVersion(It.IsAny<string>()), Times.Never);
        fileServiceMock.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
