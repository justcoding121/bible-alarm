# Busy indicator audit – modals with list view

## Expected logic

1. **Default IsBusy = true** in the ViewModel so the overlay shows immediately via XAML binding.
2. **Bind BusyOverlay.IsVisible to IsBusy** in XAML (no hardcoded `IsVisible="True"` and no code setting `BusyOverlay.IsVisible` directly).
3. **Set IsBusy = false only after**: data is loaded, set to the list, and the list is rendered (on Android, after `WaitForCollectionViewItemsRenderedAsync`).
4. **DisposeModal** receives the ViewModel so IsBusy is set to false on close and the overlay does not stick.

---

## Modals audited and updated

| Modal | ViewModel | XAML binding | Flow | DisposeModal |
|-------|-----------|--------------|------|--------------|
| **BiblePublicationLanguageModal** | BiblePublicationSelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel), DeferClearBusy | ViewModel ✓ |
| **BiblePublicationSelectionModal** | BiblePublicationSelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel), DeferClearBusy | ViewModel ✓ |
| **BiblePublicationSectionSelectionModal** | BiblePublicationSectionSelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel) | ViewModel ✓ |
| **BiblePublicationTrackSelectionModal** | BiblePublicationTrackSelectionViewModel | `IsVisible="{Binding IsBusy}"` (was `True`) | HandleModalAppearingAsync(ViewModel) | ViewModel ✓ |
| **MusicPublicationSelectionModal** | MusicPublicationSelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel) | ViewModel ✓ |
| **MusicSectionSelectionModal** | MusicSectionSelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel) | ViewModel ✓ |
| **MusicLanguageModal** | MusicPublicationSelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel) (was custom + direct IsVisible) | ViewModel ✓ |
| **MusicTrackSelectionModal** | MusicTrackSelectionViewModel | `IsVisible="{Binding IsBusy}"` (was `True`) | HandleModalAppearingAsync(ViewModel) | ViewModel ✓ |
| **MusicTypeSelectionModal** | MusicTypeSelectionViewModel | `IsVisible="{Binding IsBusy}"` (was `True`) | Custom: set ViewModel.IsBusy = false (no direct IsVisible) | ViewModel ✓ |
| **NumberOfTracksModal** | NumberOfTrackContainerViewModel | `IsVisible="{Binding IsBusy}"` (was `True`) | Custom: set ViewModel.IsBusy = false (IsBusy added to VM) | ViewModel ✓ |
| **CategorySelectionModal** | CategorySelectionViewModel | `IsVisible="{Binding IsBusy}"` | HandleModalAppearingAsync(ViewModel) | ViewModel ✓ |

---

## Code changes summary

### ModalScrollHelper

- **DisposeModal**: parameter type changed from `IListViewModel?` to `object?` so any ViewModel with an `IsBusy` property can be passed; clearing IsBusy on dispose still done via `dynamic`.
- **HandleModalAppearingAsync (IListViewModel overload)**: nullable handling in catch blocks (guard `viewModel != null` before using dynamic).

### Bible flow

- **BiblePublicationSelectionStateHandler**: `RefreshFromStateAsync` now has `clearBusyWhenDone` (default true); when false (DeferClearBusy), it does not set IsBusy false so the helper can set it after render.
- **BiblePublicationSelectionViewModel**: `DeferClearBusy` used by both language and publication modals; both pass `clearBusyWhenDone: !DeferClearBusy` to the state handler.
- **BiblePublicationSectionSelectionViewModel** / **BiblePublicationTrackSelectionViewModel**: implement `IListViewModel` (added `SelectedItem`), use HandleModalAppearingAsync(ViewModel) and pass ViewModel to DisposeModal.

### Music flow

- **MusicSectionSelectionViewModel** / **MusicTrackSelectionViewModel** / **MusicTypeSelectionViewModel**: implement `IListViewModel` (added `SelectedItem`).
- **MusicLanguageModal**: uses HandleModalAppearingAsync(ViewModel) instead of custom flow and direct `BusyOverlay.IsVisible = false`.
- **MusicTypeSelectionModal**: XAML binds to IsBusy; code sets `ViewModel.IsBusy = false` instead of `BusyOverlay.IsVisible = false`; passes ViewModel to DisposeModal.

### Schedule / shared

- **NumberOfTrackContainerViewModel**: added `IsBusy` (default true); modal sets `ViewModel.IsBusy = false` and passes ViewModel to DisposeModal.
- **CategorySelectionModal**: passes ViewModel to DisposeModal.

### XAML

- **BiblePublicationTrackSelectionModal**, **MusicTrackSelectionModal**, **MusicTypeSelectionModal**, **NumberOfTracksModal**: `BusyOverlay` changed from `IsVisible="True"` to `IsVisible="{Binding IsBusy}"`.

---

## Consistency rules

- **Binding only**: BusyOverlay visibility is driven only by `IsVisible="{Binding IsBusy}"`; no code sets `BusyOverlay.IsVisible` on these modals (except fallbacks in ModalScrollHelper on error/cancel).
- **Single clear**: For list modals using the helper, IsBusy is set to false once, after data is in the list and (on Android) after `WaitForCollectionViewItemsRenderedAsync`.
- **Dispose**: Every modal that has a list and a busy overlay passes its ViewModel (or an object with IsBusy) to `DisposeModal` so IsBusy is cleared on close.
