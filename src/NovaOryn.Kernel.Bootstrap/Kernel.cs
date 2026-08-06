using System;
using System.Runtime;
using NovaOryn.Kernel.Console;
using NovaOryn.Kernel.Platform.X64;

namespace NovaOryn.Kernel.Bootstrap;

/// <summary>Defines the authoritative freestanding NovaOryn bootstrap kernel.</summary>
public static class Kernel
{
    /// <summary>Initializes the kernel platform and enters the terminal processor halt state.</summary>
    public static Boolean KMain(BootContext boot)
    {
        if (!KernelConsole.Initialize(boot)) return false;
        if (!KernelConsole.WriteLine("NovaOryn KMain started.")) return false;
        if (!KernelPlatform.InitializeDescriptors()) return false;
        if (!KernelConsole.WriteLine("GDT and TSS installed.")) return false;
        if (!KernelPlatform.InitializeInterrupts()) return false;
        if (!KernelConsole.WriteLine("IDT with 256 vectors installed.")) return false;
        if (!KernelPlatform.DisableLegacyPic()) return false;
        if (!KernelConsole.WriteLine("Legacy PIC masked; APIC/MSI controller layer ready.")) return false;
        if (!KernelConsole.WriteLine("CPU halted.")) return false;
        return KernelPlatform.Halt();
    }

    [RuntimeExport("NovaOrynManagedEntry")]
    private static Boolean NativeEntry(UInt64 bootContextAddress)
    {
        return KMain(new BootContext(bootContextAddress));
    }
}
