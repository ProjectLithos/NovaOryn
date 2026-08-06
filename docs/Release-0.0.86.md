# NovaOryn 0.0.86

NovaOryn 0.0.86 fixes the Visual Studio project template that still generated the obsolete monolithic kernel source. The template now uses the same high-level `Kernel\Kernel.cs` and separated freestanding assemblies as `templates\NovaOrynKernel`.

The end-user kernel contains normal managed `KernelConsole.Write` and `KernelConsole.WriteLine` calls plus high-level `KernelPlatform` initialization. Native imports, x64 port I/O, framebuffer internals, the freestanding CoreLib, and the runtime entry bridge are held in separate SDK projects and compiled into separate DLLs.

The separated entry projects now reference the user kernel exactly two directories above `Sdk\NovaOryn.Kernel.Entry.X64`, correcting the previous over-traversal. `Build-NovaOryn -Project` now refreshes the selected project directory before compilation, preserves its actual root project filename, removes obsolete SDK-owned `Boot`, `Console`, `Runtime`, and `Sdk` trees, and replaces generated low-level kernel source without creating backups.

The VSIX build and source-policy tests now inspect the exact Visual Studio template payload and reject any return of `DllImport`, `Native`, `WritePort8`, `RuntimeExport`, `FramebufferConsole`, or serial-port constants in the user-owned `Kernel.cs`. They also reject the obsolete embedded `Boot`, `Console`, and `Runtime` template sources.
