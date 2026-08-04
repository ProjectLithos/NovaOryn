# NovaOryn Architecture

NovaOryn separates managed kernel policy from architecture, boot, console, runtime, compiler, linker, image, and launch concerns.

The current x64 UEFI path is:

```text
UEFI firmware
 -> native x64 EFI entry
 -> Graphics Output Protocol discovery
 -> native boot-context capture
 -> NovaOryn-owned no-CoreLib NativeAOT bootstrap
 -> managed serial/framebuffer console
 -> managed KMain
 -> repeating CLI/HLT loop
```

The native entry performs only the firmware ABI work that must occur before managed execution. Framebuffer validation, clearing, pixel-format conversion, bitmap-font rendering, cursor movement, and serial/framebuffer mirroring are managed C# responsibilities.

The ordinary SDK surface exposes the same boot data through `NovaOryn.Boot.Contracts` and the reusable framebuffer implementation through `NovaOryn.Console.Framebuffer`. Architecture-specific CPU and port operations remain in `NovaOryn.Architecture.X64`.
