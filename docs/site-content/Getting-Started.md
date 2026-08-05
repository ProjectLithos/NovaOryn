# Getting Started

Nova Oryn OS SDK compiles user-owned freestanding C# kernels with the repository-pinned .NET NativeAOT compiler.

## Create a kernel project

Install the Visual Studio extension with `Install-NovaOrynVSIX.bat`, then create a NovaOryn Kernel 0.0.43 project. The editable project is kept outside the SDK source tree.

## Kernel entry point

```csharp
[KernelEntry]
public static bool KMain(BootContext boot)
{
    // Initialise the facilities selected by your operating system.
    return CPU.Halt();
}
```

## Build and run

Use Ctrl+F5 to build and run without the debugger, or F5 for the NovaOryn debugging path. A normal command-line build uses `Build-NovaOryn.bat`; pass `-Run` through the PowerShell entry point when QEMU should start.

## Read the API reference

The Assemblies pages list every detected public item. Each API page shows its declaration, purpose, use guidance, dependencies, return contract, example and source location when those details exist in the source documentation.
