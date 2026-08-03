using NovaOryn.Primitives;

namespace NovaOryn.Boot.Contracts;

public enum BootProtocol
{
    Unknown = 0,
    Uefi = 1,
    Limine = 2,
    Multiboot2 = 3
}

public readonly record struct Framebuffer(
    PhysicalAddress Address,
    uint Width,
    uint Height,
    uint PixelsPerScanLine,
    uint BitsPerPixel)
{
    public bool IsAvailable() => Address.Value != 0 && Width != 0 && Height != 0;
}

public readonly record struct BootContext(
    BootProtocol Protocol,
    Framebuffer Framebuffer,
    PhysicalAddress MemoryMapAddress,
    ulong MemoryMapLength)
{
    public bool TryGetFramebuffer(out Framebuffer framebuffer)
    {
        framebuffer = Framebuffer;
        return framebuffer.IsAvailable();
    }
}
