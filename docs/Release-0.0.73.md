# NovaOryn 0.0.73

## Deterministic external kernel migration

The external project refresher now migrates both historical kernel locations. A recognized SDK-generated monolithic `%USERPROFILE%\Source\Repos\NovaOrynKernel\Kernel.cs` is backed up as `Kernel.cs.pre-0.0.73.bak` and removed before the clean high-level `Kernel\Kernel.cs` is installed.

The build now verifies that the obsolete root-level kernel is absent and that the user-facing `Kernel\Kernel.cs` contains no native imports, port I/O, runtime-export bridge, framebuffer internals, or low-level `Native` class. A genuinely user-authored root-level kernel is never silently deleted; the refresh stops with a clear migration instruction.
