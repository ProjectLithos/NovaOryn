using NovaOryn.Core;

namespace NovaOryn.Architecture.X64;

public static class CPU
{
    public static bool EnableInterrupts() => NativeMethods.EnableInterrupts();
    public static bool DisableInterrupts() => NativeMethods.DisableInterrupts();
    public static bool AreInterruptsEnabled() => NativeMethods.AreInterruptsEnabled();

    [DoesNotReturn]
    public static bool Halt() => NativeMethods.Halt();
}

public static class Port
{
    public static bool Write8(ushort port, byte value) => NativeMethods.WritePort8(port, value);
    public static bool TryRead8(ushort port, out byte value) => NativeMethods.ReadPort8(port, out value);
}
