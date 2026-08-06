using System;
using System.Runtime.InteropServices;

namespace NovaOryn.Kernel.Internal.X64;

/// <summary>Contains the private x64 native ABI used by managed kernel services.</summary>
public static class Native
{
    /// <summary>Initializes the x64 COM1 serial device used by the managed console.</summary>
    public static Boolean InitializeSerial()
    {
        if (!WritePort8(0x3F9, 0x00)) return false;
        if (!WritePort8(0x3FB, 0x80)) return false;
        if (!WritePort8(0x3F8, 0x01)) return false;
        if (!WritePort8(0x3F9, 0x00)) return false;
        if (!WritePort8(0x3FB, 0x03)) return false;
        if (!WritePort8(0x3FA, 0xC7)) return false;
        return WritePort8(0x3FC, 0x0B);
    }

    /// <summary>Writes one byte to the initialized x64 COM1 serial device.</summary>
    public static Boolean WriteSerial(Byte value) => WritePort8(0x3F8, value);

    /// <summary>Installs the bootstrap processor GDT and TSS.</summary>
    [DllImport("*", EntryPoint = "NovaOrynX64InitializeBootstrapDescriptors", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InitializeBootstrapDescriptors();

    /// <summary>Installs the bootstrap processor IDT.</summary>
    [DllImport("*", EntryPoint = "NovaOrynX64InitializeBootstrapInterrupts", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InitializeBootstrapInterrupts();

    /// <summary>Masks the two legacy 8259 PIC controllers.</summary>
    [DllImport("*", EntryPoint = "NovaOrynX64DisableLegacyPic", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean DisableLegacyPic();

    /// <summary>Stops the current processor permanently.</summary>
    [DllImport("*", EntryPoint = "NovaOrynX64Halt", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean Halt();

    [DllImport("*", EntryPoint = "NovaOrynX64WritePort8", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern Boolean WritePort8(UInt16 port, Byte value);
}
