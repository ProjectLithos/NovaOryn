using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace NovaOryn.Kernel.Bootstrap;

public readonly struct BootContext
{
    public readonly UInt64 Signature;
}

internal static class Native
{
    [DllImport("*", EntryPoint = "NovaOrynX64WritePort8", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern Boolean WritePort8(UInt16 port, Byte value);

    [DllImport("*", EntryPoint = "NovaOrynX64Halt", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern Boolean Halt();
}

public static class Kernel
{
    public static Boolean KMain(BootContext boot)
    {
        if (!InitializeSerial()) return false;
        if (!WriteText()) return false;
        return Native.Halt();
    }

    [RuntimeExport("NovaOrynManagedEntry")]
    private static Boolean NativeEntry(IntPtr bootContext)
    {
        BootContext context = default;
        return KMain(context);
    }

    private static Boolean InitializeSerial()
    {
        if (!Native.WritePort8(0x3F9, 0x00)) return false;
        if (!Native.WritePort8(0x3FB, 0x80)) return false;
        if (!Native.WritePort8(0x3F8, 0x01)) return false;
        if (!Native.WritePort8(0x3F9, 0x00)) return false;
        if (!Native.WritePort8(0x3FB, 0x03)) return false;
        if (!Native.WritePort8(0x3FA, 0xC7)) return false;
        return Native.WritePort8(0x3FC, 0x0B);
    }

    private static Boolean WriteText()
    {
        if (!WriteLineNovaOrynStarted()) return false;
        return WriteLineCpuHalted();
    }

    private static Boolean WriteLineNovaOrynStarted()
    {
        if (!Write((Byte)'N')) return false;
        if (!Write((Byte)'o')) return false;
        if (!Write((Byte)'v')) return false;
        if (!Write((Byte)'a')) return false;
        if (!Write((Byte)'O')) return false;
        if (!Write((Byte)'r')) return false;
        if (!Write((Byte)'y')) return false;
        if (!Write((Byte)'n')) return false;
        if (!Write((Byte)' ')) return false;
        if (!Write((Byte)'K')) return false;
        if (!Write((Byte)'M')) return false;
        if (!Write((Byte)'a')) return false;
        if (!Write((Byte)'i')) return false;
        if (!Write((Byte)'n')) return false;
        if (!Write((Byte)' ')) return false;
        if (!Write((Byte)'s')) return false;
        if (!Write((Byte)'t')) return false;
        if (!Write((Byte)'a')) return false;
        if (!Write((Byte)'r')) return false;
        if (!Write((Byte)'t')) return false;
        if (!Write((Byte)'e')) return false;
        if (!Write((Byte)'d')) return false;
        if (!Write((Byte)'.')) return false;
        if (!Write((Byte)'\r')) return false;
        return Write((Byte)'\n');
    }

    private static Boolean WriteLineCpuHalted()
    {
        if (!Write((Byte)'C')) return false;
        if (!Write((Byte)'P')) return false;
        if (!Write((Byte)'U')) return false;
        if (!Write((Byte)' ')) return false;
        if (!Write((Byte)'h')) return false;
        if (!Write((Byte)'a')) return false;
        if (!Write((Byte)'l')) return false;
        if (!Write((Byte)'t')) return false;
        if (!Write((Byte)'e')) return false;
        if (!Write((Byte)'d')) return false;
        if (!Write((Byte)'.')) return false;
        if (!Write((Byte)'\r')) return false;
        return Write((Byte)'\n');
    }

    private static Boolean Write(Byte value) => Native.WritePort8(0x3F8, value);
}
