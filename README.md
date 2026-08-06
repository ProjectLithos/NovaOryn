# Nova Oryn OS SDK 0.0.60

Nova Oryn OS SDK (`NovaOryn`) is a from-scratch SDK for compiling user-owned freestanding C# kernels and operating systems with the real .NET NativeAOT compiler (`ilc`).

## Release 0.0.60

Release 0.0.60 adds a transport-neutral interrupt-controller contract and staged x64 PIC, Local APIC, I/O APIC, MSI, MSI-X, and x2APIC implementation. Drivers route, mask, acknowledge, prioritize, and retarget interrupts without knowing the underlying delivery mechanism.

Release 0.0.59 added production x64 Global Descriptor Table and Task State Segment foundations. It provides public GDT/TSS contracts, a per-processor managed implementation, native LGDT/segment-reload/LTR wrappers, emergency IST stack configuration, and explicit I/O permission bitmap policy.

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

`Build-NovaOryn.bat` creates it through the compiled `NovaOryn.ProjectCreator` tool when it does not already exist. The SDK remains in `C:\NovaOryn`; generated EFI, image and run artifacts remain under `C:\NovaOryn\Artifacts`. Existing kernel files are not overwritten.

Open `NovaOrynKernel.sln` in Visual Studio to edit `Kernel.cs`. The default build consumes `NovaOrynProject.json` from this external project directory while preserving the framebuffer, serial and halt behaviour completed in the preceding releases.

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

See `docs/Release-0.0.60.md` for this release.


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

Run `Install-NovaOrynVSIX.bat`, then create a **NovaOryn Kernel 0.0.60** project in Visual Studio. F5 and Ctrl+F5 invoke the NovaOryn build-and-run pipeline.

## Kernel project layout

Generated kernel projects place boot contracts, console code, kernel entry code and runtime support into `Boot`, `Console`, `Kernel` and `Runtime` subdirectories.


## SDK usage documentation

Run `Build-NovaOrynDocumentation.bat` to regenerate the offline site at `Artifacts\Documentation\site\index.html`. The normal `Build-NovaOryn.bat` entry point regenerates the site before compiling the SDK and kernel. Public API documentation uses standard XML comments together with `<nova.when>` and `<nova.depends>` metadata.

## IDT and CPU exceptions

Version 0.0.60 adds all 256 x64 IDT vectors, a normalised managed/native exception frame, driver-vector allocation, handler lifecycle management, IST-aware essential exception handlers, and terminal fatal handling.


Version 0.0.60 adds controller-neutral IRQ routing, legacy PIC takeover, APIC EOI/IPI, I/O APIC redirection, MSI/MSI-X message creation, x2APIC delivery, affinity, priorities, polarity, trigger mode, and masking.
