# NovaOryn 0.0.71

NovaOryn 0.0.71 corrects the separate x64 managed entry bridge after the low-level implementation was removed from the end-user `Kernel.cs`.

## Correction

`KernelEntry.cs` now calls the boot kernel with the fully qualified type name:

```csharp
global::NovaOryn.Kernel.Bootstrap.Kernel.KMain(new BootContext(bootContextAddress))
```

This prevents C# from resolving `NovaOryn.Kernel` as a namespace when compiling `NovaOryn.Kernel.Entry.X64.dll`. The same correction is present in the authoritative source and the Visual Studio/end-user project template.

## End-user boundary

The end-user `Kernel.cs` remains high-level managed C# only. Runtime export glue remains in `NovaOryn.Kernel.Entry.X64.dll`, and native imports and low-level I/O remain in `NovaOryn.Kernel.X64.LowLevel.dll`.
