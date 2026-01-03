#nullable enable

namespace Bible.Alarm.Stores.Actions.Schedule;

/// <summary>
/// Dispatched by schedule page containers when they are ready (initialized from state and rendered).
/// </summary>
public record ContainerReadyAction
{
    public string ContainerName { get; init; }

    public ContainerReadyAction(string containerName)
    {
        ContainerName = containerName;
    }
}

