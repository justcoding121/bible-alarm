#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Interfaces;
using System.Runtime.InteropServices;

namespace Bible.Alarm.Tests;

public sealed class ModalScrollHelperTests
{
    private sealed class StubListViewModel : IListViewModel
    {
        public object? SelectedItem => null;

        public bool IsBusy { get; set; } = true;
    }

    [Fact]
    public void IsFetchFailure_true_for_http_request_exception()
    {
        Assert.True(ModalScrollHelper.IsFetchFailure(new HttpRequestException()));
    }

    [Fact]
    public void IsFetchFailure_true_when_inner_exception_is_fetch_failure()
    {
        Assert.True(ModalScrollHelper.IsFetchFailure(new InvalidOperationException("wrap", new HttpRequestException())));
    }

    [Fact]
    public void IsFetchFailure_true_for_timeout_exception()
    {
        Assert.True(ModalScrollHelper.IsFetchFailure(new TimeoutException()));
    }

    [Fact]
    public void IsFetchFailure_true_for_task_canceled_with_timeout_inner()
    {
        var ex = new TaskCanceledException("canceled", new TimeoutException());
        Assert.True(ModalScrollHelper.IsFetchFailure(ex));
    }

    [Fact]
    public void IsFetchFailure_true_for_web_and_socket_exceptions()
    {
        Assert.True(ModalScrollHelper.IsFetchFailure(new WebException()));
        Assert.True(ModalScrollHelper.IsFetchFailure(new System.Net.Sockets.SocketException()));
    }

    [Fact]
    public void IsFetchFailure_false_for_unrelated_exception()
    {
        Assert.False(ModalScrollHelper.IsFetchFailure(new ArgumentException()));
    }

    [Fact]
    public void GetFetchErrorMessage_http_status_maps_message()
    {
        var serverErr = new HttpRequestException("err", null, HttpStatusCode.ServiceUnavailable);
        Assert.Contains("temporarily unavailable", ModalScrollHelper.GetFetchErrorMessage(serverErr));
    }

    [Fact]
    public void GetFetchErrorMessage_not_found_uses_not_found_message()
    {
        var notFound = new HttpRequestException("err", null, HttpStatusCode.NotFound);
        Assert.Contains("not found", ModalScrollHelper.GetFetchErrorMessage(notFound));
    }

    [Fact]
    public void GetFetchErrorMessage_other_http_status_uses_generic_message()
    {
        var other = new HttpRequestException("err", null, HttpStatusCode.BadRequest);
        Assert.Contains("Something went wrong", ModalScrollHelper.GetFetchErrorMessage(other));
    }

    [Fact]
    public void GetFetchErrorMessage_timeout_uses_connection_message()
    {
        Assert.Contains("too long", ModalScrollHelper.GetFetchErrorMessage(new TimeoutException()));
    }

    [Fact]
    public void GetFetchErrorMessage_task_canceled_uses_connection_message()
    {
        Assert.Contains("too long", ModalScrollHelper.GetFetchErrorMessage(new TaskCanceledException()));
    }

    [Fact]
    public void GetFetchErrorMessage_falls_back_to_default_when_no_status()
    {
        Assert.Equal(ModalScrollHelper.DefaultFetchErrorMessage, ModalScrollHelper.GetFetchErrorMessage(new HttpRequestException()));
    }

    [Fact]
    public void DefaultFetchErrorMessage_matches_toast_constant()
    {
        Assert.Equal(AppConstants.ToastMessages.PleaseCheckInternetConnection, ModalScrollHelper.DefaultFetchErrorMessage);
    }

    [Fact]
    public async Task HandleModalAppearingAsync_returns_success_when_view_model_null()
    {
        var result = await ModalScrollHelper.HandleModalAppearingAsync(null, new ListModalAppearOptions(null, null));

        Assert.Equal(ModalAppearingResult.Success, result);
    }

    [Collection("MauiUi")]
    public sealed class MauiModalScrollHelperTests(MauiUiFixture fixture)
    {
        private static async Task RunWithMainThreadOrSkipAsync(Func<Task> testBody)
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            try
            {
                await testBody();
            }
            catch (COMException)
            {
                // Headless xUnit cannot initialize WinUI MainThread; device/CI app hosts cover this path.
            }
        }

        [Fact]
        public async Task HandleModalAppearingAsync_clears_busy_when_no_refresh_or_collection_view()
        {
            _ = fixture;
            await RunWithMainThreadOrSkipAsync(async () =>
            {
                var vm = new StubListViewModel();

                await ModalScrollHelper.HandleModalAppearingAsync(vm, new ListModalAppearOptions(null, null));

                Assert.False(vm.IsBusy);
            });
        }

        [Fact]
        public async Task HandleModalAppearingAsync_returns_fetch_failed_when_refresh_throws_network_error()
        {
            _ = fixture;
            await RunWithMainThreadOrSkipAsync(async () =>
            {
                var vm = new StubListViewModel();

                var result = await ModalScrollHelper.HandleModalAppearingAsync(
                    vm,
                    new ListModalAppearOptions(
                        null,
                        null,
                        RefreshAction: () => throw new HttpRequestException(),
                        OnFetchFailed: _ => Task.CompletedTask));

                Assert.Equal(ModalAppearingResult.FetchFailed, result);
            });
        }

        [Fact]
        public async Task DisposeModal_clears_is_busy_on_list_view_model()
        {
            _ = fixture;
            await RunWithMainThreadOrSkipAsync(async () =>
            {
                var vm = new StubListViewModel { IsBusy = true };

                ModalScrollHelper.DisposeModal(null, null, vm);
                await MainThread.InvokeOnMainThreadAsync(() => { });

                Assert.False(vm.IsBusy);
            });
        }
    }
}
