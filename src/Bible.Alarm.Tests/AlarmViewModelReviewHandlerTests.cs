#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class AlarmViewModelReviewHandlerTests
{
    private sealed class StubReviewPromptService : IReviewPromptService
    {
        public int RecordDismissCalls;
        public Exception? ThrowFromDismiss;

        public Task RecordAppOpenAsync() => Task.CompletedTask;

        public Task RecordDismissEngagementAndRequestIfEligibleAsync()
        {
            RecordDismissCalls++;
            if (ThrowFromDismiss is not null)
            {
                throw ThrowFromDismiss;
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleReviewRequestAsync_calls_review_flow_once()
    {
        var logger = TestLogging.CreateLogger();
        var service = new StubReviewPromptService();
        var sut = new AlarmViewModelReviewHandler(logger, service);

        await sut.HandleReviewRequestAsync();

        Assert.Equal(1, service.RecordDismissCalls);
    }

    [Fact]
    public async Task HandleReviewRequestAsync_swallows_review_service_errors()
    {
        var logger = TestLogging.CreateLogger();
        var service = new StubReviewPromptService { ThrowFromDismiss = new InvalidOperationException("store error") };
        var sut = new AlarmViewModelReviewHandler(logger, service);

        await sut.HandleReviewRequestAsync();

        Assert.Equal(1, service.RecordDismissCalls);
    }
}
