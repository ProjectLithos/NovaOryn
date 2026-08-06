# NovaOryn 0.0.72

## Existing external kernel migration

A normal SDK build now refreshes an existing `%USERPROFILE%\Source\Repos\NovaOrynKernel` project through `NovaOryn.ProjectCreator`. SDK-owned support assemblies are refreshed, and a recognized SDK-generated monolithic `Kernel\Kernel.cs` is backed up and replaced by the high-level template. User-authored kernels that do not match a known generated form remain untouched.

The end-user `Kernel.cs` contains no native imports, runtime export bridge, serial port addresses, framebuffer implementation, or character-by-character output.
