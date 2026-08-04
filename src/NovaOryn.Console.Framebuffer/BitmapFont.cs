namespace NovaOryn.Console.Framebuffer;

internal static class BitmapFont
{
    internal static ulong GetGlyph(char value) => value switch
    {
        'N' => 0x47359C631UL, 'o' => 0x01D18C62EUL, 'v' => 0x02318C544UL,
        'a' => 0x01C17C66DUL, 'O' => 0x3A318C62EUL, 'r' => 0x02D984210UL,
        'y' => 0x02317862EUL, 'n' => 0x02D98C631UL, 'K' => 0x4654C5251UL,
        'M' => 0x4775AC631UL, 'i' => 0x100C2108EUL, 's' => 0x01F0707C0UL,
        't' => 0x211E42126UL, 'e' => 0x01D1FC22EUL, 'd' => 0x042D9C66DUL,
        '.' => 0x00000018CUL, 'C' => 0x3A308422EUL, 'P' => 0x7A31F4210UL,
        'U' => 0x46318C62EUL, 'h' => 0x4216CC631UL, 'l' => 0x30842108EUL,
        ' ' => 0UL, _ => 0x3A2111004UL
    };
}
