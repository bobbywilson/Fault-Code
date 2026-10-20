using FaultCode.Application;

namespace FaultCode.Infrastructure;

public sealed class RuntimeEnvironment(string name) : IRuntimeEnvironment
{
    public string Name { get; } = name;
}
