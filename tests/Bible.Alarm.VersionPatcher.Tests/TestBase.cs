using AutoFixture;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests;

public abstract class TestBase
{
    protected readonly IFixture fixture = new Fixture();
    protected readonly MockRepository mockRepository = new(MockBehavior.Strict);

    protected T Create<T>() => fixture.Create<T>();
    protected Mock<T> CreateMock<T>() where T : class => mockRepository.Create<T>();
}
