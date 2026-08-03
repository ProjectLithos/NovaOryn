# NovaOryn architecture

The authoritative build path is:

```text
User C# + selected NovaOryn assemblies
    -> Roslyn
    -> managed IL
    -> real ilc / NativeAOT
    -> native objects
    -> NovaOryn.Linker.exe
    -> kernel ELF
    -> NovaOryn.ImageBuilder.exe
    -> bootable UEFI image
```

`KMain` is the user-owned managed kernel entry point. The native entry object performs only machine and runtime transition work before calling the NativeAOT export that represents `KMain`.

The Oryn microkernel is not part of release 0.0.4 and will remain optional.
