# Nova Oryn OS SDK 0.0.16

Nova Oryn OS SDK (`NovaOryn`) is a from-scratch SDK for compiling user-owned freestanding C# kernels and operating systems with the real .NET NativeAOT compiler (`ilc`).

## Release 0.0.16

This release fixes NativeAOT acquisition by allowing the .NET SDK to resolve its `DotnetPlatform` runtime packs instead of directly referencing them as ordinary NuGet packages.

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

See `docs/Release-0.0.16.md` for this release.


## 0.0.16 build

Run `Build-NovaOryn.bat` after the source update and toolchain installation. It invokes the real NativeAOT/ILC static-library pipeline and then the NovaOryn native linker.

## 0.0.16 build correction

Managed solution projects build as `Any CPU`; x64 is selected only for the NativeAOT kernel and native toolchain stages.
