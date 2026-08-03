using NovaOryn.Architecture.X64;
using NovaOryn.Boot.Contracts;
using NovaOryn.Console.Serial;
using NovaOryn.Core;

namespace NovaOryn.Kernel.Sample;

public static class Kernel
{
    [KernelEntry]
    public static bool KMain(BootContext boot)
    {
        SerialConsole console = new();
        if (!console.Configure(SerialConfiguration.Com1())) return false;
        if (!console.Initialize(boot)) return false;
        if (!console.WriteLine("NovaOryn 0.0.3 KMain started.")) return false;
        if (!console.WriteLine("Real ILC integration is the next acceptance milestone.")) return false;
        return CPU.Halt();
    }
}
