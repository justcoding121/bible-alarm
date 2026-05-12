#nullable enable

using System.Collections;
using System.Runtime.InteropServices;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class CollectionViewScrollExecutorTests
{
    [Fact]
    public void FindItemIndexByValue_finds_language_by_code_when_instance_differs()
    {
        var lang = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var rowInList = new LanguageListViewItemModel(lang, "English");
        var lookupInstance = new LanguageListViewItemModel(lang, "Other display");
        var list = new ArrayList { rowInList };

        var index = CollectionViewScrollExecutor.FindItemIndexByValue(list, lookupInstance);

        Assert.Equal(0, index);
    }

    [Fact]
    public void FindItemIndexByValue_returns_negative_one_when_no_value_match()
    {
        var langA = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var langF = new Language { LanguageCode = "F", Direction = AppConstants.Media.TextDirectionLeftToRight };
        var list = new ArrayList { new LanguageListViewItemModel(langA, "English") };
        var other = new LanguageListViewItemModel(langF, "Français");

        var index = CollectionViewScrollExecutor.FindItemIndexByValue(list, other);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void ShouldLogUnexpectedScrollFailure_is_false_for_expected_platform_exceptions()
    {
        Assert.False(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new OperationCanceledException()));
        Assert.False(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new NullReferenceException()));
        Assert.False(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new COMException()));
    }

    [Fact]
    public void ShouldLogUnexpectedScrollFailure_is_true_for_unexpected_errors()
    {
        Assert.True(CollectionViewScrollExecutor.ShouldLogUnexpectedScrollFailure(new InvalidOperationException()));
    }
}
