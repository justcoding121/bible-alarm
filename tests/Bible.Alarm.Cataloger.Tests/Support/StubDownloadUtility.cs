#nullable enable

using Bible.Alarm.Cataloger.Utility;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

internal sealed class StubDownloadUtility : DownloadUtility
{
    private readonly Func<string, Task<string>> respond;

    public StubDownloadUtility(ILogger logger, Func<string, Task<string>> respond)
        : base(logger)
    {
        this.respond = respond;
    }

    internal override Task<string> GetAsync(string catalogLink) => respond(catalogLink);
}
