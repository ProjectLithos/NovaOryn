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
        if (!Write((Byte)'0')) return false;
        if (!Write((Byte)'.')) return false;
        if (!Write((Byte)'0')) return false;
        if (!Write((Byte)'.')) return false;
        if (!Write((Byte)'2')) return false;
        if (!Write((Byte)'0')) return false;
        if (!Write((Byte)'\r')) return false;
        return Write((Byte)'\n');
    }

    private static Boolean Write(Byte value) => Native.WritePort8(0x3F8, value);
}
