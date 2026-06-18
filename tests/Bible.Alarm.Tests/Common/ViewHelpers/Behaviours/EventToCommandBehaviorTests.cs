#nullable enable

using System.Windows.Input;
using Bible.Alarm.Common.ViewHelpers.Behaviours;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class EventToCommandBehaviorTests(MauiUiFixture fixture)
{
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> ExecutedParameters { get; } = [];
        public bool CanExecuteResult { get; set; } = true;
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => CanExecuteResult;

        public void Execute(object? parameter) => ExecutedParameters.Add(parameter);
    }

    [Fact]
    public void OnAttachedTo_throws_when_event_name_is_missing()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var button = new Button();
        var behavior = new EventToCommandBehavior();

        Assert.Throws<ArgumentException>(() => button.Behaviors.Add(behavior));
    }

    [Fact]
    public void OnAttachedTo_throws_when_named_event_does_not_exist()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var button = new Button();
        var behavior = new EventToCommandBehavior { EventName = "MissingEvent" };

        Assert.Throws<ArgumentException>(() => button.Behaviors.Add(behavior));
    }

    [Fact]
    public void Clicked_event_executes_bound_command_with_command_parameter()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var command = new RecordingCommand();
        var button = new Button();
        var behavior = new EventToCommandBehavior
        {
            EventName = nameof(Button.Clicked),
            Command = command,
            CommandParameter = "tap",
        };

        button.Behaviors.Add(behavior);
        behavior.OnFired(button, EventArgs.Empty);

        Assert.Single(command.ExecutedParameters);
        Assert.Equal("tap", command.ExecutedParameters[0]);
    }

    [Fact]
    public void Clicked_event_does_not_execute_when_can_execute_is_false()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var command = new RecordingCommand { CanExecuteResult = false };
        var button = new Button();
        var behavior = new EventToCommandBehavior
        {
            EventName = nameof(Button.Clicked),
            Command = command,
        };

        button.Behaviors.Add(behavior);
        behavior.OnFired(button, EventArgs.Empty);

        Assert.Empty(command.ExecutedParameters);
    }

    [Fact]
    public void Detaching_unsubscribes_from_event()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var command = new RecordingCommand();
        var button = new Button();
        var behavior = new EventToCommandBehavior
        {
            EventName = nameof(Button.Clicked),
            Command = command,
        };

        button.Behaviors.Add(behavior);
        button.Behaviors.Remove(behavior);
        behavior.OnFired(button, EventArgs.Empty);

        Assert.Empty(command.ExecutedParameters);
    }
}
