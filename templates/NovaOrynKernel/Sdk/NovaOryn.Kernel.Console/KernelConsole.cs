using System;
using NovaOryn.Kernel.Internal.X64;

namespace NovaOryn.Kernel.Console;

/// <summary>Provides normal managed C# console output for a freestanding NovaOryn kernel.</summary>
public static class KernelConsole
{
    private static FramebufferConsole _framebuffer;
    private static Boolean _initialized;

    /// <summary>Initializes serial and framebuffer output.</summary>
    public static Boolean Initialize(BootContext boot)
    {
        if (!Native.InitializeSerial()) return false;
        if (!_framebuffer.Initialize(boot)) return false;
        if (!_framebuffer.Clear()) return false;
        _initialized = true;
        return true;
    }

    /// <summary>Writes a managed string without appending a line terminator.</summary>
    public static Boolean Write(String value)
    {
        if (!_initialized || value == null) return false;
        Int32 length = value.Length;
        Int32 index = 0;
        while (index < length)
        {
            Char character = value[index];
            if ((UInt32)character > 0x7FU) character = (Char)'?';
            if (!Write((Byte)character)) return false;
            index++;
        }
        return true;
    }

    /// <summary>Writes a managed string followed by a carriage return and line feed.</summary>
    public static Boolean WriteLine(String value)
    {
        if (!Write(value)) return false;
        if (!Write((Byte)'\r')) return false;
        return Write((Byte)'\n');
    }

    /// <summary>Writes one character to every configured console target.</summary>
    public static Boolean Write(Byte value)
    {
        if (!_initialized) return false;
        if (!Native.WriteSerial(value)) return false;
        return _framebuffer.Write(value);
    }
}
