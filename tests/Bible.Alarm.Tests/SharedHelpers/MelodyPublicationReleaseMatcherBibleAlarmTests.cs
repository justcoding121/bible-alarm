#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class MelodyPublicationReleaseMatcherBibleAlarmTests
{
    [Fact]
    public void MatchesReleaseKeys_returns_false_when_publication_code_missing()
    {
        Assert.False(MelodyPublicationReleaseMatcher.MatchesReleaseKeys(["iam"], null));
    }

    [Fact]
    public void MatchesReleaseKeys_matches_case_insensitively()
    {
        Assert.True(MelodyPublicationReleaseMatcher.MatchesReleaseKeys(["IAM"], "iam"));
    }

    [Fact]
    public void MatchesReleaseKeys_returns_false_when_no_key_equals_publication_code()
    {
        Assert.False(MelodyPublicationReleaseMatcher.MatchesReleaseKeys(["other"], "iam"));
    }
}
