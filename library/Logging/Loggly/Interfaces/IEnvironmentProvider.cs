namespace Loggly
{
    public interface IEnvironmentProvider
    {
        int ProcessId { get; }

        string MachineName { get; }
    }
}
