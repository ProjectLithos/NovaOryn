using NovaOryn.Boot.Contracts;
using NovaOryn.Console.Contracts;

namespace NovaOryn.Console.Framebuffer;

public readonly struct FramebufferConfiguration
{
    public FramebufferConfiguration(
        byte foregroundRed,
        byte foregroundGreen,
        byte foregroundBlue,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue,
        uint scale,
        uint margin)
    {
        ForegroundRed = foregroundRed;
        ForegroundGreen = foregroundGreen;
        ForegroundBlue = foregroundBlue;
        BackgroundRed = backgroundRed;
        BackgroundGreen = backgroundGreen;
        BackgroundBlue = backgroundBlue;
        Scale = scale;
        Margin = margin;
    }

    public byte ForegroundRed { get; }
    public byte ForegroundGreen { get; }
    public byte ForegroundBlue { get; }
    public byte BackgroundRed { get; }
    public byte BackgroundGreen { get; }
    public byte BackgroundBlue { get; }
    public uint Scale { get; }
    public uint Margin { get; }

    public static FramebufferConfiguration Default()
    {
        return new FramebufferConfiguration(232, 240, 248, 9, 16, 24, 2, 16);
    }
}

public sealed unsafe class FramebufferConsole : IConsole
{
    private FramebufferConfiguration _configuration;
    private Framebuffer _framebuffer;
    private uint _cursorX;
    private uint _cursorY;
    private uint _foreground;
    private uint _background;
    private bool _initialized;

    public bool Configure(FramebufferConfiguration configuration)
    {
        if (configuration.Scale == 0 || configuration.Scale > 8)
            throw new ArgumentOutOfRangeException(nameof(configuration));
        _configuration = configuration;
        return true;
    }

    public bool Initialize(BootContext boot)
    {
        if (!boot.TryGetFramebuffer(out Framebuffer framebuffer)) return false;
        if (_configuration.Scale == 0 && !Configure(FramebufferConfiguration.Default())) return false;
        _framebuffer = framebuffer;
        if (_configuration.Margin >= framebuffer.Width || _configuration.Margin >= framebuffer.Height) return false;
        _cursorX = _configuration.Margin;
        _cursorY = _configuration.Margin;
        _foreground = PackColor(_configuration.ForegroundRed, _configuration.ForegroundGreen, _configuration.ForegroundBlue);
        _background = PackColor(_configuration.BackgroundRed, _configuration.BackgroundGreen, _configuration.BackgroundBlue);
        _initialized = true;
        return Clear();
    }

    public bool Clear()
    {
        if (!_initialized) return false;
        ulong pixelCount = checked((ulong)_framebuffer.PixelsPerScanLine * _framebuffer.Height);
        if (pixelCount > _framebuffer.SizeInBytes / 4UL) return false;
        uint* pixel = (uint*)_framebuffer.Address.Value;
        while (pixelCount != 0)
        {
            *pixel++ = _background;
            pixelCount--;
        }
        _cursorX = _configuration.Margin;
        _cursorY = _configuration.Margin;
        return true;
    }

    public bool Write(ReadOnlySpan<char> text)
    {
        if (!_initialized) return false;
        foreach (char character in text)
        {
            if (!WriteCharacter(character)) return false;
        }
        return true;
    }

    public bool WriteLine(ReadOnlySpan<char> text) => Write(text) && WriteLine();
    public bool WriteLine() => Write("\r\n");

    private bool WriteCharacter(char value)
    {
        if (value == '\r') return true;
        if (value == '\n') return MoveToNextLine();
        uint scale = _configuration.Scale;
        uint width = 5U * scale;
        if (_cursorX + width >= _framebuffer.Width && !MoveToNextLine()) return false;
        if (_cursorY + (7U * scale) >= _framebuffer.Height) return false;
        if (!DrawGlyph(value, _cursorX, _cursorY)) return false;
        _cursorX += width + (2U * scale);
        return true;
    }

    private bool MoveToNextLine()
    {
        _cursorX = _configuration.Margin;
        _cursorY += 10U * _configuration.Scale;
        return _cursorY + (7U * _configuration.Scale) < _framebuffer.Height;
    }

    private bool DrawGlyph(char value, uint originX, uint originY)
    {
        ulong glyph = BitmapFont.GetGlyph(value);
        for (uint row = 0; row < 7; row++)
        {
            uint bits = (uint)((glyph >> (int)((6U - row) * 5U)) & 0x1FUL);
            for (uint column = 0; column < 5; column++)
            {
                if ((bits & (1U << (int)(4U - column))) == 0) continue;
                if (!DrawBlock(originX + (column * _configuration.Scale), originY + (row * _configuration.Scale))) return false;
            }
        }
        return true;
    }

    private bool DrawBlock(uint originX, uint originY)
    {
        for (uint y = 0; y < _configuration.Scale; y++)
        {
            for (uint x = 0; x < _configuration.Scale; x++)
            {
                uint pixelX = originX + x;
                uint pixelY = originY + y;
                ulong index = checked(((ulong)pixelY * _framebuffer.PixelsPerScanLine) + pixelX);
                if (index >= _framebuffer.SizeInBytes / 4UL) return false;
                *((uint*)_framebuffer.Address.Value + index) = _foreground;
            }
        }
        return true;
    }

    private uint PackColor(byte red, byte green, byte blue)
    {
        return _framebuffer.PixelFormat switch
        {
            FramebufferPixelFormat.RedGreenBlueReserved8BitPerColor => red | ((uint)green << 8) | ((uint)blue << 16),
            FramebufferPixelFormat.BlueGreenRedReserved8BitPerColor => blue | ((uint)green << 8) | ((uint)red << 16),
            FramebufferPixelFormat.BitMask => EncodeMask(red, _framebuffer.PixelMask.Red) |
                                              EncodeMask(green, _framebuffer.PixelMask.Green) |
                                              EncodeMask(blue, _framebuffer.PixelMask.Blue),
            _ => 0U
        };
    }

    private static uint EncodeMask(byte component, uint mask)
    {
        int shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        int bits = System.Numerics.BitOperations.PopCount(mask);
        ulong maximum = bits == 32 ? uint.MaxValue : ((1UL << bits) - 1UL);
        return ((uint)(((ulong)component * maximum) / byte.MaxValue) << shift) & mask;
    }
}
