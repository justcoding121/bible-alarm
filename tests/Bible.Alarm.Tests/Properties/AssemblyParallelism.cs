using Xunit;

// xharness device hosts set MaxParallelThreads=1, but xunit still schedules test collections in
// parallel unless parallelization is disabled here. Android CI logcat showed multiple managed threads
// executing tests concurrently (e.g. MiniPlaybackBarViewModelTests on threads 2858 and 2864), which
// deadlocks on shared MiniPlaybackBarViewModel.Instance and MAUI MainThread queues.
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
