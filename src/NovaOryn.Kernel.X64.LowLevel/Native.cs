using System;
using System.Runtime.InteropServices;

namespace NovaOryn.Kernel.Internal.X64;

/// <summary>Contains the private x64 native ABI used by managed kernel services.</summary>
public static class Native
{
    /// <summary>Writes one byte to an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "NovaOrynX64WritePort8", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WritePort8(UInt16 port, Byte value);

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
}
