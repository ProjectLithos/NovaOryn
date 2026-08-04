using System.Runtime.InteropServices;
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
        if (!console.WriteLine("NovaOryn 0.0.15 KMain started.")) return false;
        if (!console.WriteLine("KMain was compiled by the real NativeAOT ILC pipeline.")) return false;
        return CPU.Halt();
    }

    [UnmanagedCallersOnly(EntryPoint = "NovaOrynManagedEntry")]
    public static byte NativeEntry(nint bootContextAddress)
    {
        BootContext boot = bootContextAddress == 0
            ? default
            : Marshal.PtrToStructure<BootContext>(bootContextAddress);
        return KMain(boot) ? (byte)1 : (byte)0;
    }
}
