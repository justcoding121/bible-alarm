#nullable enable

using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;

namespace Bible.Alarm.Services.UI.NavigationServiceHelpers;

internal enum LanguageModalKind
{
    Bible,
    Music,
}

internal static class LanguageModalBindingClassifier
{
    internal static LanguageModalKind ClassifyBindingContextRuntimeType(Type bindingContextType)
    {
        if (bindingContextType == typeof(BiblePublicationSelectionViewModel))
        {
            return LanguageModalKind.Bible;
        }

        if (bindingContextType == typeof(MusicPublicationSelectionViewModel))
        {
            return LanguageModalKind.Music;
        }

        throw new ArgumentException($"Unsupported ViewModel type: {bindingContextType.Name}", nameof(bindingContextType));
    }
}
