using System;

namespace NovaOryn.Kernel.Bootstrap;

#pragma warning disable CS0649 // Populated by the native UEFI entry before managed execution.
internal struct NativeBootContext
{
    internal UInt64 Signature;
    internal UInt64 FramebufferAddress;
    internal UInt64 FramebufferSize;
    internal UInt32 Width;
    internal UInt32 Height;
    internal UInt32 PixelsPerScanLine;
    internal UInt32 PixelFormat;
    internal UInt32 RedMask;
    internal UInt32 GreenMask;
    internal UInt32 BlueMask;
    internal UInt32 ReservedMask;
}
#pragma warning restore CS0649

public readonly unsafe struct BootContext
{
    private readonly UInt64 _nativeAddress;

    internal BootContext(UInt64 nativeAddress)
    {
        _nativeAddress = nativeAddress;
    }

    internal NativeBootContext* GetNativeContext()
    {
        return (NativeBootContext*)_nativeAddress;
    }

    public Boolean IsAvailable()
    {
        NativeBootContext* context = GetNativeContext();
        return context != null && context->Signature == 0x4E59524F41564F4EUL;
    }

    public UInt64 GetFramebufferAddress()
    {
        NativeBootContext* context = GetNativeContext();
        return context == null ? 0UL : context->FramebufferAddress;
    }

    public UInt64 GetFramebufferSize()
    {
        NativeBootContext* context = GetNativeContext();
        return context == null ? 0UL : context->FramebufferSize;
    }

    public UInt32 GetFramebufferWidth()
    {
        NativeBootContext* context = GetNativeContext();
        return context == null ? 0U : context->Width;
    }

    public UInt32 GetFramebufferHeight()
    {
        NativeBootContext* context = GetNativeContext();
        return context == null ? 0U : context->Height;
    }

    public UInt32 GetFramebufferPitchInPixels()
    {
        NativeBootContext* context = GetNativeContext();
        return context == null ? 0U : context->PixelsPerScanLine;
    }

    public UInt32 GetFramebufferPixelFormat()
    {
        NativeBootContext* context = GetNativeContext();
        return context == null ? 3U : context->PixelFormat;
    }
}
