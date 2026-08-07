using System;
using NovaOryn.Kernel.Console;
using NovaOryn.Kernel.Platform.X64;

namespace NovaOryn.Kernel.Bootstrap;

/// <summary>Defines the user-owned NovaOryn kernel.</summary>
public static class Kernel
{
    private const UInt32 ConsoleFontSize = 32U;

    /// <summary>Initializes the kernel platform and enters the terminal processor halt state.</summary>
    public static Boolean KMain(BootContext boot)
    {
        if (!KernelConsole.Initialize(boot, ConsoleFontSize)) return false;
        if (!KernelConsole.WriteLine("NovaOryn KMain started.")) return false;

        // USER CODE: change this text, add more managed calls, then rebuild.
        if (!KernelConsole.WriteLine("Editable kernel: change this line and rebuild.")) return false;
        if (!boot.HasFinalMemoryMap()) return false;
        if (!KernelConsole.WriteLine("Final UEFI memory map retained; ExitBootServices succeeded.")) return false;
        if (!KernelPlatform.InitializeDescriptors()) return false;
        if (!KernelConsole.WriteLine("GDT and TSS installed.")) return false;
        if (!KernelPlatform.InitializeInterrupts()) return false;
        if (!KernelConsole.WriteLine("IDT with 256 vectors installed.")) return false;
        if (!KernelPlatform.DisableLegacyPic()) return false;
        if (!KernelConsole.WriteLine("Legacy PIC masked; APIC/MSI controller layer ready.")) return false;
        if (!KernelConsole.WriteLine("CPU halted.")) return false;
        return KernelPlatform.Halt();
    }
}
