using System;

namespace NovaOryn.Kernel.Console;

internal unsafe struct FramebufferConsole
{
    private UInt64 _address;
    private UInt64 _size;
    private UInt32 _width;
    private UInt32 _height;
    private UInt32 _pitch;
    private UInt32 _pixelFormat;
    private UInt32 _redMask;
    private UInt32 _greenMask;
    private UInt32 _blueMask;
    private UInt32 _cursorX;
    private UInt32 _cursorY;
    private UInt32 _fontSize;
    private UInt32 _glyphWidth;
    private UInt32 _characterAdvance;
    private UInt32 _lineHeight;
    private UInt32 _margin;
    private UInt32 _foreground;
    private UInt32 _background;

    internal UInt32 FontSize
    {
        get { return _fontSize; }
    }

    internal Boolean Initialize(BootContext boot, UInt32 fontSize)
    {
        NativeBootContext* context = boot.GetNativeContext();
        if (context == null || context->Signature != 0x4E59524F41564F4EUL) return false;
        if (context->FramebufferAddress == 0 || (context->FramebufferAddress & 3UL) != 0) return false;
        if (context->FramebufferSize == 0 || context->Width == 0 || context->Height == 0) return false;
        if (context->PixelsPerScanLine < context->Width) return false;
        if (context->PixelFormat > 2U) return false;

        UInt64 bytesPerScanLine = (UInt64)context->PixelsPerScanLine * 4UL;
        if (bytesPerScanLine == 0 || (UInt64)context->Height > context->FramebufferSize / bytesPerScanLine) return false;
        if (context->PixelFormat == 2U)
        {
            if (context->RedMask == 0 || context->GreenMask == 0 || context->BlueMask == 0) return false;
            if ((context->RedMask & context->GreenMask) != 0) return false;
            if ((context->RedMask & context->BlueMask) != 0) return false;
            if ((context->GreenMask & context->BlueMask) != 0) return false;
            if (context->ReservedMask != 0U)
            {
                if ((context->ReservedMask & context->RedMask) != 0) return false;
                if ((context->ReservedMask & context->GreenMask) != 0) return false;
                if ((context->ReservedMask & context->BlueMask) != 0) return false;
            }
            if (!IsContiguousMask(context->RedMask)) return false;
            if (!IsContiguousMask(context->GreenMask)) return false;
            if (!IsContiguousMask(context->BlueMask)) return false;
        }

        if (BitmapFont.GetFontContractVersion() != 2U) return false;
        if (fontSize < BitmapFont.MinimumFontSize || fontSize > BitmapFont.MaximumFontSize) return false;
        UInt32 glyphWidth = BitmapFont.GetRenderedGlyphWidth(fontSize);
        UInt32 characterAdvance = BitmapFont.GetRenderedCharacterAdvance(fontSize);
        UInt32 lineHeight = BitmapFont.GetRenderedLineHeight(fontSize);
        UInt32 margin = fontSize / 2U;
        if (margin == 0U) margin = 1U;
        if (glyphWidth == 0U || characterAdvance < glyphWidth || lineHeight < fontSize) return false;
        if (margin >= context->Width || margin >= context->Height) return false;
        if (glyphWidth > context->Width - margin || fontSize > context->Height - margin) return false;

        _address = context->FramebufferAddress;
        _size = context->FramebufferSize;
        _width = context->Width;
        _height = context->Height;
        _pitch = context->PixelsPerScanLine;
        _pixelFormat = context->PixelFormat;
        _redMask = context->RedMask;
        _greenMask = context->GreenMask;
        _blueMask = context->BlueMask;
        _fontSize = fontSize;
        _glyphWidth = glyphWidth;
        _characterAdvance = characterAdvance;
        _lineHeight = lineHeight;
        _margin = margin;
        _cursorX = margin;
        _cursorY = margin;
        _foreground = PackColor(232, 240, 248);
        _background = PackColor(9, 16, 24);
        return true;
    }

    internal Boolean Clear()
    {
        UInt64 pixelCount = (UInt64)_pitch * (UInt64)_height;
        if (pixelCount > _size / 4UL) return false;

        UInt32* pixel = (UInt32*)_address;
        while (pixelCount != 0)
        {
            *pixel = _background;
            pixel++;
            pixelCount--;
        }
        _cursorX = _margin;
        _cursorY = _margin;
        return true;
    }

    internal Boolean Write(Byte value)
    {
        if (value == (Byte)'\r') return true;
        if (value == (Byte)'\n') return MoveToNextLine();
        if (_cursorX > _width - _glyphWidth && !MoveToNextLine()) return false;
        if (_cursorY > _height - _fontSize) return false;
        if (!DrawGlyph(value, _cursorX, _cursorY)) return false;
        _cursorX += _characterAdvance;
        return true;
    }

    private Boolean MoveToNextLine()
    {
        _cursorX = _margin;
        if (_cursorY > 0xFFFFFFFFU - _lineHeight) return false;
        _cursorY += _lineHeight;
        return _cursorY <= _height - _fontSize;
    }

    private Boolean DrawGlyph(Byte value, UInt32 originX, UInt32 originY)
    {
        UInt32 renderedRow = 0;
        while (renderedRow < _fontSize)
        {
            UInt32 sourceRow = BitmapFont.GetSourceRow(renderedRow, _fontSize);
            UInt32 bits = BitmapFont.GetGlyphRow(value, sourceRow);
            UInt32 renderedColumn = 0;
            while (renderedColumn < _glyphWidth)
            {
                UInt32 sourceColumn = BitmapFont.GetSourceColumn(renderedColumn, _glyphWidth);
                UInt32 mask = 1U << (Int32)((BitmapFont.GlyphWidth - 1U) - sourceColumn);
                if ((bits & mask) != 0U)
                {
                    if (!DrawPixel(originX + renderedColumn, originY + renderedRow)) return false;
                }
                renderedColumn++;
            }
            renderedRow++;
        }
        return true;
    }

    private Boolean DrawPixel(UInt32 pixelX, UInt32 pixelY)
    {
        if (pixelX >= _width || pixelY >= _height) return false;
        UInt64 index = ((UInt64)pixelY * (UInt64)_pitch) + pixelX;
        if (index >= _size / 4UL) return false;
        *((UInt32*)_address + index) = _foreground;
        return true;
    }

    private UInt32 PackColor(Byte red, Byte green, Byte blue)
    {
        if (_pixelFormat == 0U)
        {
            return (UInt32)red | ((UInt32)green << 8) | ((UInt32)blue << 16);
        }
        if (_pixelFormat == 1U)
        {
            return (UInt32)blue | ((UInt32)green << 8) | ((UInt32)red << 16);
        }
        return EncodeMask(red, _redMask) | EncodeMask(green, _greenMask) | EncodeMask(blue, _blueMask);
    }

    private static Boolean IsContiguousMask(UInt32 mask)
    {
        while ((mask & 1U) == 0U) mask >>= 1;
        while ((mask & 1U) != 0U) mask >>= 1;
        return mask == 0U;
    }

    private static UInt32 EncodeMask(Byte component, UInt32 mask)
    {
        UInt32 shift = 0;
        while (((mask >> (Int32)shift) & 1U) == 0U && shift < 31U) shift++;
        UInt32 shiftedMask = mask >> (Int32)shift;
        UInt32 bits = 0;
        while ((shiftedMask & 1U) != 0U)
        {
            bits++;
            shiftedMask >>= 1;
        }
        UInt64 maximum = bits == 32U ? 0xFFFFFFFFUL : ((1UL << (Int32)bits) - 1UL);
        UInt32 encoded = (UInt32)(((UInt64)component * maximum) / 255UL);
        return (encoded << (Int32)shift) & mask;
    }
}
