using System.Runtime.InteropServices;
using NovaOryn.Architecture.X64;
using NovaOryn.Boot.Contracts;
using NovaOryn.Console.Framebuffer;
using NovaOryn.Console.Serial;
using NovaOryn.Core;
using NovaOryn.Primitives;

namespace NovaOryn.Kernel.Sample;

public static class Kernel
{
    [KernelEntry]
    public static bool KMain(BootContext boot)
    {
        SerialConsole serial = new();
        FramebufferConsole framebuffer = new();
        if (!serial.Configure(SerialConfiguration.Com1())) return false;
        if (!framebuffer.Configure(FramebufferConfiguration.Default())) return false;
        if (!serial.Initialize(boot)) return false;
        if (!framebuffer.Initialize(boot)) return false;
        if (!WriteLine(serial, framebuffer, "NovaOryn KMain started.")) return false;
        if (!WriteLine(serial, framebuffer, "CPU halted.")) return false;
        return CPU.Halt();
    }

    [UnmanagedCallersOnly(EntryPoint = "NovaOrynManagedEntry")]
    public static unsafe byte NativeEntry(nint bootContextAddress)
    {
        if (bootContextAddress == 0) return 0;
        NativeBootContext* native = (NativeBootContext*)bootContextAddress;
        if (native->Signature != 0x4E59524F41564F4EUL) return 0;

        Framebuffer framebuffer = new(
            new PhysicalAddress(native->FramebufferAddress),
            native->FramebufferSize,
            native->Width,
            native->Height,
            native->PixelsPerScanLine,
            (FramebufferPixelFormat)native->PixelFormat,
            new PixelBitMask(native->RedMask, native->GreenMask, native->BlueMask, native->ReservedMask));
        BootContext boot = new(BootProtocol.Uefi, framebuffer, default, 0);
        return KMain(boot) ? (byte)1 : (byte)0;
    }

    private static bool WriteLine(SerialConsole serial, FramebufferConsole framebuffer, ReadOnlySpan<char> text)
    {
        if (!serial.WriteLine(text)) return false;
        return framebuffer.WriteLine(text);
    }

    #pragma warning disable CS0649 // Populated by the native UEFI entry before managed execution.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBootContext
    {
        internal ulong Signature;
        internal ulong FramebufferAddress;
        internal ulong FramebufferSize;
        internal uint Width;
        internal uint Height;
        internal uint PixelsPerScanLine;
        internal uint PixelFormat;
        internal uint RedMask;
        internal uint GreenMask;
        internal uint BlueMask;
        internal uint ReservedMask;
    }
    #pragma warning restore CS0649
}
