using AutoFixture;
using FluentAssertions;
using Moq;

namespace Bible.Alarm.VersionPatcher.Tests;

public abstract class TestBase
{
    protected readonly IFixture Fixture;
    protected readonly MockRepository MockRepository;

    protected TestBase()
    {
        Fixture = new Fixture();
        MockRepository = new MockRepository(MockBehavior.Strict);
    }

    protected T Create<T>() => Fixture.Create<T>();
    protected Mock<T> CreateMock<T>() where T : class => MockRepository.Create<T>();
}
