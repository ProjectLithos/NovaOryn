using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace NovaOryn.Kernel.Bootstrap;

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

        FramebufferConsole framebuffer = default;
        if (!framebuffer.Initialize(boot)) return false;
        if (!framebuffer.Clear()) return false;
        if (!WriteText(ref framebuffer)) return false;
        return Native.Halt();
    }

    [RuntimeExport("NovaOrynManagedEntry")]
    private static Boolean NativeEntry(UInt64 bootContextAddress)
    {
        BootContext context = new BootContext(bootContextAddress);
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

    private static Boolean WriteText(ref FramebufferConsole framebuffer)
    {
        if (!WriteLineNovaOrynStarted(ref framebuffer)) return false;
        return WriteLineCpuHalted(ref framebuffer);
    }

    private static Boolean WriteLineNovaOrynStarted(ref FramebufferConsole framebuffer)
    {
        if (!Write(ref framebuffer, (Byte)'N')) return false;
        if (!Write(ref framebuffer, (Byte)'o')) return false;
        if (!Write(ref framebuffer, (Byte)'v')) return false;
        if (!Write(ref framebuffer, (Byte)'a')) return false;
        if (!Write(ref framebuffer, (Byte)'O')) return false;
        if (!Write(ref framebuffer, (Byte)'r')) return false;
        if (!Write(ref framebuffer, (Byte)'y')) return false;
        if (!Write(ref framebuffer, (Byte)'n')) return false;
        if (!Write(ref framebuffer, (Byte)' ')) return false;
        if (!Write(ref framebuffer, (Byte)'K')) return false;
        if (!Write(ref framebuffer, (Byte)'M')) return false;
        if (!Write(ref framebuffer, (Byte)'a')) return false;
        if (!Write(ref framebuffer, (Byte)'i')) return false;
        if (!Write(ref framebuffer, (Byte)'n')) return false;
        if (!Write(ref framebuffer, (Byte)' ')) return false;
        if (!Write(ref framebuffer, (Byte)'s')) return false;
        if (!Write(ref framebuffer, (Byte)'t')) return false;
        if (!Write(ref framebuffer, (Byte)'a')) return false;
        if (!Write(ref framebuffer, (Byte)'r')) return false;
        if (!Write(ref framebuffer, (Byte)'t')) return false;
        if (!Write(ref framebuffer, (Byte)'e')) return false;
        if (!Write(ref framebuffer, (Byte)'d')) return false;
        if (!Write(ref framebuffer, (Byte)'.')) return false;
        if (!Write(ref framebuffer, (Byte)'\r')) return false;
        return Write(ref framebuffer, (Byte)'\n');
    }

    private static Boolean WriteLineCpuHalted(ref FramebufferConsole framebuffer)
    {
        if (!Write(ref framebuffer, (Byte)'C')) return false;
        if (!Write(ref framebuffer, (Byte)'P')) return false;
        if (!Write(ref framebuffer, (Byte)'U')) return false;
        if (!Write(ref framebuffer, (Byte)' ')) return false;
        if (!Write(ref framebuffer, (Byte)'h')) return false;
        if (!Write(ref framebuffer, (Byte)'a')) return false;
        if (!Write(ref framebuffer, (Byte)'l')) return false;
        if (!Write(ref framebuffer, (Byte)'t')) return false;
        if (!Write(ref framebuffer, (Byte)'e')) return false;
        if (!Write(ref framebuffer, (Byte)'d')) return false;
        if (!Write(ref framebuffer, (Byte)'.')) return false;
        if (!Write(ref framebuffer, (Byte)'\r')) return false;
        return Write(ref framebuffer, (Byte)'\n');
    }

    private static Boolean Write(ref FramebufferConsole framebuffer, Byte value)
    {
        if (!Native.WritePort8(0x3F8, value)) return false;
        return framebuffer.Write(value);
    }
}
