#nullable enable

namespace Bible.Alarm.Tests.Support;

[CollectionDefinition("MauiUi", DisableParallelization = true)]
public sealed class MauiUiTestCollectionDefinition : ICollectionFixture<MauiUiFixture>;
