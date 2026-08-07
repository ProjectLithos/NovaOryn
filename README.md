# Nova Oryn OS SDK 0.0.97

Nova Oryn OS SDK (`NovaOryn`) is a from-scratch SDK for compiling user-owned freestanding C# kernels and operating systems with the real .NET NativeAOT compiler (`ilc`).

## Release 0.0.97

Release 0.0.97 repairs incomplete ChangedFiles installations that can otherwise leave the repository without `THIRD-PARTY-NOTICES.md`. The notice is deliberately revised and reissued in this release, so applying 0.0.97 restores the required root file even when an earlier incremental update omitted it.

Source-policy validation now checks for the notice file before reading it and reports a normal test failure rather than terminating with `FileNotFoundException`. The updater also validates every file, size, and SHA-256 recorded by the target `NovaOryn-SourceManifest.json` after extraction and deletion/rename processing, before the source is committed or pushed. This prevents a future ChangedFiles update from silently producing an incomplete source tree.

## Release 0.0.95

Release 0.0.95 corrects the freestanding framebuffer font dispatch seen at runtime as repeated question-mark glyphs for valid ASCII letters, digits, and punctuation. The font data itself was complete, but the two dense 95-case glyph switches were not a safe dispatch form for this freestanding NativeAOT path.

Both packed glyph halves are now compile-time constants selected through explicit range checks and bit branches. The renderer therefore reaches every printable ASCII glyph without a switch jump table, while keeping the exact NovaOryn Mono 8×16 data and the user-selected rendered `FontSize`. The authoritative kernel console, command-line project template, Visual Studio template, and reusable framebuffer assembly use the same glyph values.

The generated kernel remains user-owned: edit only `Kernel\Kernel.cs`, then run the project build or run wrapper. SDK support code is refreshed separately and must not replace that file. Source-policy validation now rejects a return to dense font switches and verifies both halves of all 95 printable characters in the constant table.

## Release 0.0.94

Release 0.0.94 replaces the framebuffer console's partial 5×7 character table with **NovaOryn Mono**, a complete embedded bitmap typeface for printable ASCII U+0020 through U+007E.

The font now provides proper uppercase and lowercase letters, digits, punctuation, symbols, and below-baseline descenders for `g`, `j`, `p`, `q`, and `y`. Its embedded raster master is 8×16, but the renderer accepts one real `FontSize` value—the rendered glyph height in framebuffer pixels—and derives glyph width, character advance, line height, wrapping, and sampling from that value. Generated kernels explicitly request 32 pixels with `KernelConsole.Initialize(boot, 32U)`, and both framebuffer APIs report the exact accepted size. There is no separate public scale setting.

Source-policy validation checks all 95 printable characters, both packed halves of every glyph, renderer/template identity, reusable/freestanding data identity, and descender rows. A specimen is included at `docs/assets/NovaOryn-Mono-8x16.png`, and the typeface notice is retained in `THIRD-PARTY-NOTICES.md`.

## Release 0.0.93

Release 0.0.93 corrects the source-policy test build failure discovered when 0.0.92 was compiled with the repository-pinned Windows toolchain. The boot-context validation for framebuffer fields now uses a distinct `framebufferBootContext` local instead of redeclaring the existing `bootstrapBootContext` local in the same top-level scope.

The boot memory-map contracts, final UEFI map capture, and all three normalisation implementations introduced in 0.0.92 are unchanged. This release exists so the complete solution and source-policy test project can compile before the memory tests and native kernel stages run.

## Release 0.0.92

Release 0.0.92 implements the boot memory-map foundation. The native x64 UEFI entry now retains the exact final map obtained immediately before a successful `ExitBootServices`, retries a stale map key without allocating or making another firmware call in between, and passes final-map metadata into managed bootstrap.

`NovaOryn.Memory.Contracts` defines the portable descriptor, memory ownership, cache attributes, runtime status, availability, future NUMA metadata, reservations, bounded workspaces, results, and immutable diagnostic cursor. `NovaOryn.Boot.Memory` translates UEFI descriptors and implements three complete normalisation versions:

- **Strict** rejects incompatible firmware overlaps.
- **Safety priority** chooses the most restrictive known owner.
- **Conservative** converts incompatible firmware overlaps to reserved memory while retaining firmware runtime ownership.

Every version sorts ranges, rejects overflow and invalid alignment, splits at every boundary, overlays kernel/boot/framebuffer/MMIO/page-table/early-allocation reservations without discarding firmware runtime ownership, reduces conflicting cache modes to one safe mode, preserves mixed runtime code/data ownership, and merges metadata-compatible adjacent ranges.

## Release 0.0.91

Release 0.0.91 fixes freestanding CoreLib compilation after the real NativeAOT string layout was introduced. The `_stringLength` and `_firstChar` fields are populated by NativeAOT when it materializes string objects, so the three authoritative CoreLib copies now use a narrowly scoped `CS0649` suppression instead of allowing warnings-as-errors to reject the runtime-owned layout.

## Release 0.0.56

Release 0.0.56 documents `ICpu`, corrects the stale internal Visual Studio project-template version, and adds build-time validation to prevent VSIX/template version drift.

## Release 0.0.53

Release 0.0.53 fixes the CS1591 failures in `NovaOryn.Core/KernelAttributes.cs`, restores that file to FullSource, and corrects stale Visual Studio/QEMU displayed versions. The VSIX installer fix from 0.0.52 remains included.

Release 0.0.52 fixed `Install-NovaOrynVSIX.ps1` so it reads the current extension identity and version from the VSIX manifest and installs the artifact that was just built. It no longer selects the obsolete hard-coded 0.0.44 package. The installer also detects a running Visual Studio instance and explains Windows interruption status `0xC000013A`.

## Release 0.0.51

Release 0.0.51 introduces the first formal architecture boundary. `NovaOryn.Architecture.Contracts` defines architecture-neutral lifecycle, feature, memory-barrier and page-table concepts. `NovaOryn.Architecture.X64` provides a static, zero-allocation hot-path API bound to native assembly function pointers, while `X64CpuArchitecture` supplies the higher-level lifecycle contract. `NovaOryn.Architecture.Arm64` is an explicit non-operational scaffold that reports failure until its native backend is implemented; it never pretends ARM64 support is ready.

The x64 layer owns control-register and MSR access, interrupt control, halt and pause, timestamp reads, atomic compare/exchange, port I/O, memory barriers, page-table entry encoding, context switching and CPU feature detection. Architecture-neutral SDK code no longer needs to expose x64 encodings for these operations.

## External C# kernel project

The editable C# kernel project is created automatically at:

```text
C:\Users\<UserName>\Source\Repos\NovaOrynKernel
```

`Build-NovaOryn.bat` refreshes this project through the compiled `NovaOryn.ProjectCreator` tool. SDK-owned support assemblies are updated, recognised generated monolithic kernels are removed, and a genuinely user-authored `Kernel\Kernel.cs` is preserved. The SDK remains in `C:\NovaOryn`; generated EFI, image and run artifacts remain under `C:\NovaOryn\Artifacts`.

Open `NovaOrynKernel.sln` in Visual Studio to edit `Kernel\Kernel.cs`. A normal SDK build compiles the authoritative in-repository bootstrap. Visual Studio Build/Run commands pass the selected project manifest explicitly. In either path, the user kernel contains high-level managed calls while native I/O, framebuffer implementation, runtime entry glue, and architecture initialization remain in separate SDK assemblies.

## Source update workflow

Run:

```text
Update-NovaOryn.bat
```

or provide the folder containing the release archives:

```text
Update-NovaOryn.bat D:\NovaOryn-Releases
```

The updater performs this order:

```text
First repository update:
    latest FullSource ZIP
    -> C:\NovaOryn
    -> initial Git commit
    -> push main to origin
    -> toolchain validation/install

Later repository updates:
    latest ChangedFiles ZIP
    -> C:\NovaOryn
    -> apply deletion/rename manifest
    -> update Git commit
    -> push main to origin
    -> toolchain validation/install
```

If the GitHub push fails, no toolchain is downloaded.

## Toolchain

`Install-NovaOrynToolchain.bat` is normally invoked automatically after a successful source push. It can also be run directly after the repository is clean and committed.

The pinned components are declared in `toolchain/NovaOryn.Toolchain.json`:

- .NET SDK 10.0.312
- NativeAOT/ILC packages 10.0.10
- LLD and selected LLVM binary utilities 22.1.6

### LLVM assembler policy

`llvm-mc.exe` is optional. NovaOryn uses NASM for required x64 assembly, so toolchain installation does not fail when the official LLVM Windows package omits `llvm-mc.exe`. LLD and the listed LLVM binary utilities remain mandatory.
- QEMU 11.0.0 or newer
- NASM 2.16.0 or newer

ILC compiles the managed kernel. LLD links the ILC objects and NovaOryn native assets into the freestanding kernel. The full Clang compiler is not part of the managed compilation path.

Repository-local tools and packages are stored below `C:\NovaOryn\.toolchain` and are excluded from Git.

## Kernel entry point

```csharp
[KernelEntry]
public static bool KMain(BootContext boot)
```

## Build policy

Kernel and OS creation is performed by NovaOryn executable tools. Scripts may bootstrap and validate the development toolchain, but they do not translate, link, or package the kernel.

The updater now accepts exact NovaOryn files left uncommitted from earlier releases when their SHA-256 values match the existing source manifest. Unrelated local edits are still rejected.

See `docs/Release-0.0.97.md` for this release.


## 0.0.22 build

Run `Build-NovaOryn.bat` after the source update and toolchain installation. It invokes the real NativeAOT/ILC static-library pipeline and then the NovaOryn native linker.

## 0.0.22 build correction

Managed solution projects build as `Any CPU`; x64 is selected only for the NativeAOT kernel and native toolchain stages.


## 0.0.22 runtime boundary

The minimal kernel now uses `NovaOryn.RuntimePack.X64.Bootstrap`, a NovaOryn-owned no-standard-library NativeAOT system module. The build does not link the stock Windows CoreLib or Windows NativeAOT runtime libraries.

## NativeAOT compiler RID

The dedicated bootstrap publish currently uses `win-x64` only to select the Microsoft .NET SDK's x64 ILC compiler assets on the Windows build host. The ordinary kernel sample no longer carries a runtime identifier. The produced EFI image does not link Windows CoreLib or Windows platform runtime libraries.
## 0.0.22 custom CoreLib correction

`System.Object._methodTable` is part of the minimal NativeAOT object layout. Generated native code consumes it even though ordinary C# source does not, so the field now has a local `CS0169` suppression instead of weakening repository-wide warning checks.


## 0.0.24 direct ILC correction

The `ResolvedILCompilerPack` failure is eliminated because the bootstrap project no longer imports NativeAOT publish targets. `NovaOryn.ManagedCompiler.exe` performs two explicit stages:

```text
Roslyn build -> NovaOryn.Kernel.Bootstrap.dll
pinned ilc.exe -> MinimalKernel.obj
```

The `win-x64` name now appears only in the path of the ILC executable that runs on the Windows development host. ILC is passed `--targetos:win` solely to produce PE/COFF code using the Microsoft x64 ABI required by UEFI. No Windows CoreLib or NativeAOT runtime library is linked.


## 0.0.24 .NET 10 ILC scanner correction

The direct bootstrap compiler now passes `--noscan`, `--reflectiondata:none`, and `--nopreinitstatics`. The previous `-O` switch implied the IL scanner, which eagerly requested `System.Buffer` and would subsequently request other full-CoreLib helpers that are outside the no-GC bootstrap contract. NovaOryn also defines the required `System.Buffer.BulkMoveWithWriteBarrier` fail-fast boundary so an accidental managed-reference bulk copy halts rather than corrupting memory.


## 0.0.25 compilation manifest correction

`NovaOryn.ManagedCompiler.exe` emits schema 5 because the direct-ILC manifest records the scanner mode, optimisation policy, ILC executable, compiler-host RID, and direct native object. `NovaOryn.Linker.exe` now accepts exactly schema 5 and reports both the received and supported schema when they differ. A source-policy regression test keeps the writer and reader aligned.

## 0.0.26 boot-and-run stage

`NovaOryn.ImageBuilder.exe` now writes the disk image itself. It creates a protective MBR, primary and backup GPT headers, an EFI System Partition, a FAT32 volume, and the required removable-media path:

```text
EFI\BOOT\BOOTX64.EFI
```

`Install-NovaOrynToolchain.bat` locates the non-secure x64 OVMF code image and writable variable-store template shipped with QEMU. Their resolved paths are recorded in `.toolchain\NovaOryn.ToolPaths.json`.

`NovaOryn.QemuLauncher.exe` copies the boot image and OVMF variable store into a unique run directory, starts QEMU immediately without `-S`, watches the serial log for both acceptance lines, confirms the QEMU process remains alive, writes `NovaOryn.Run.json`, and returns without terminating the VM.

## 0.0.31 framebuffer build correction

Release 0.0.31 corrects the C# namespace/type collision in `NovaOryn.Console.Framebuffer`. The framebuffer contract type is now referenced through the explicit `BootFramebuffer` alias, allowing the managed framebuffer assembly to compile without changing its public namespace or API.

## 0.0.27 managed framebuffer console

`native\x64\Entry.asm` calls UEFI `LocateProtocol` for the Graphics Output Protocol before interrupts are disabled. It records the current framebuffer in a compact native boot context and passes its address to `NovaOrynManagedEntry`.

The no-CoreLib bootstrap validates the context and framebuffer bounds before touching video memory. It supports the UEFI RGB, BGR, and direct bit-mask pixel formats, clears the full visible framebuffer, and renders the acceptance output with a managed bitmap font. Serial output is performed first for each character, so framebuffer work cannot remove the existing COM1 diagnostics.

The SDK also contains the reusable `NovaOryn.Console.Framebuffer` assembly and the ordinary kernel sample demonstrates explicit serial/framebuffer mirroring.


## Visual Studio

Run `Install-NovaOrynVSIX.bat`, then create a **NovaOryn Kernel 0.0.97** project in Visual Studio. F5 and Ctrl+F5 invoke the NovaOryn build-and-run pipeline.

## Kernel project layout

Generated kernel projects expose the user-owned source only under `Kernel`. Freestanding CoreLib, console implementation, native entry glue, x64 platform services, and native interop are compiled as separate SDK assemblies under the project’s hidden `Sdk` support tree.


## SDK usage documentation

Run `Build-NovaOrynDocumentation.bat` to regenerate the offline site at `Artifacts\Documentation\site\index.html`. The normal `Build-NovaOryn.bat` entry point regenerates the site before compiling the SDK and kernel. Public API documentation uses standard XML comments together with `<nova.when>` and `<nova.depends>` metadata.

## IDT and CPU exceptions

NovaOryn provides all 256 x64 IDT vectors, a normalised managed/native exception frame, driver-vector allocation, handler lifecycle management, IST-aware essential exception handlers, and terminal fatal handling.


NovaOryn provides controller-neutral IRQ routing, legacy PIC takeover, APIC EOI/IPI, I/O APIC redirection, MSI/MSI-X message creation, x2APIC delivery, affinity, priorities, polarity, trigger mode, and masking.
