using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Tests;

public sealed class CatalogTypeTests
{
    [Fact]
    public void Underlying_values_remain_stable_for_persistence_and_api_contracts()
    {
        Assert.Equal(0, (int)CatalogType.Flat);
        Assert.Equal(1, (int)CatalogType.Sectioned);
        Assert.Equal(2, (int)CatalogType.MediatorSectioned);
        Assert.Equal(3, (int)CatalogType.IssueSectioned);
    }
}
