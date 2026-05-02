#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Common.ViewHelpers;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Tests;

public sealed class ModalScrollHelperTests
{
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

        var notFound = new HttpRequestException("err", null, HttpStatusCode.NotFound);
        Assert.Contains("not found", ModalScrollHelper.GetFetchErrorMessage(notFound));

        var other = new HttpRequestException("err", null, HttpStatusCode.BadRequest);
        Assert.Contains("Something went wrong", ModalScrollHelper.GetFetchErrorMessage(other));
    }

    [Fact]
    public void GetFetchErrorMessage_timeout_uses_connection_message()
    {
        Assert.Contains("too long", ModalScrollHelper.GetFetchErrorMessage(new TimeoutException()));
        Assert.Contains("too long", ModalScrollHelper.GetFetchErrorMessage(new TaskCanceledException()));
    }

    [Fact]
    public void GetFetchErrorMessage_falls_back_to_default_when_no_status()
    {
        Assert.Equal(ModalScrollHelper.DefaultFetchErrorMessage, ModalScrollHelper.GetFetchErrorMessage(new HttpRequestException()));
        Assert.Equal(AppConstants.ToastMessages.PleaseCheckInternetConnection, ModalScrollHelper.DefaultFetchErrorMessage);
    }
}
