using AutoFixture;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests;

public abstract class TestBase
{
    protected readonly IFixture Fixture = new Fixture();
    protected readonly MockRepository MockRepository = new(MockBehavior.Strict);

    protected T Create<T>() => Fixture.Create<T>();
    protected Mock<T> CreateMock<T>() where T : class => MockRepository.Create<T>();
}
