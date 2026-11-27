using Xunit;

// One emulator and one Appium session. Parallel collections would fight over the same device.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
