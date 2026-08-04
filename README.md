# Nova Oryn OS SDK 0.0.26

Nova Oryn OS SDK (`NovaOryn`) is a from-scratch SDK for compiling user-owned freestanding C# kernels and operating systems with the real .NET NativeAOT compiler (`ilc`).

## Release 0.0.26

This release implements the first complete x64 UEFI boot-and-run stage. The build now links the freestanding NativeAOT EFI application, creates a real GPT disk image with a FAT32 EFI System Partition, installs the kernel at `EFI/BOOT/BOOTX64.EFI`, launches QEMU with x64 OVMF without `-S`, captures COM1 serial output, confirms managed `KMain`, confirms `CPU.Halt()`, and leaves the halted VM open.

The required runtime acceptance output is:

```text
NovaOryn KMain started.
CPU halted.
```

`Build-NovaOryn.bat` now performs build, image creation, and runtime acceptance by default. Use `Build-NovaOryn.bat -NoRun` to stop after producing `Artifacts\MinimalKernel\MinimalKernel.img`.

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

- .NET SDK 10.0.302
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

See `docs/Release-0.0.26.md` for this release.


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

