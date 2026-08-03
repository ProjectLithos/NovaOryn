using NovaOryn.Core;
using NovaOryn.Primitives;

namespace NovaOryn.Architecture.Contracts;

public interface ICpu
{
    bool EnableInterrupts();
    bool DisableInterrupts();
    bool AreInterruptsEnabled();
    ProcessorId GetProcessorId();

    [DoesNotReturn]
    bool Halt();
}
