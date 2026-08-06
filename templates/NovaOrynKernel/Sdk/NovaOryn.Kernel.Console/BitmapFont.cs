using System;

namespace NovaOryn.Kernel.Console;

internal static class BitmapFont
{
    internal static UInt64 GetGlyph(Byte value)
    {
        switch (value)
        {
            case (Byte)'N': return 0x47359C631UL;
            case (Byte)'o': return 0x01D18C62EUL;
            case (Byte)'v': return 0x02318C544UL;
            case (Byte)'a': return 0x01C17C66DUL;
            case (Byte)'O': return 0x3A318C62EUL;
            case (Byte)'r': return 0x02D984210UL;
            case (Byte)'y': return 0x02317862EUL;
            case (Byte)'n': return 0x02D98C631UL;
            case (Byte)'K': return 0x4654C5251UL;
            case (Byte)'M': return 0x4775AC631UL;
            case (Byte)'i': return 0x100C2108EUL;
            case (Byte)'s': return 0x01F0707C0UL;
            case (Byte)'t': return 0x211E42126UL;
            case (Byte)'e': return 0x01D1FC22EUL;
            case (Byte)'d': return 0x042D9C66DUL;
            case (Byte)'.': return 0x00000018CUL;
            case (Byte)'C': return 0x3A308422EUL;
            case (Byte)'P': return 0x7A31F4210UL;
            case (Byte)'U': return 0x46318C62EUL;
            case (Byte)'h': return 0x4216CC631UL;
            case (Byte)'l': return 0x30842108EUL;
            case (Byte)' ': return 0UL;
            default: return 0x3A2111004UL;
        }
    }
}
