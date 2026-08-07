# Editable NovaOryn kernel

This project requires **NovaOryn SDK 0.0.96 or later**.

Edit `Kernel\Kernel.cs`. Do not move the file and do not add native imports to it. The line marked `USER CODE` is safe to change immediately.

Build without starting QEMU:

```text
Build-Kernel.bat
```

Build and start QEMU:

```text
Run-Kernel.bat
```

The wrappers use `C:\NovaOryn` by default. Set the `NOVAORYN_SDK_ROOT` environment variable when the SDK is installed elsewhere.

The `Sdk` directory contains generated implementation support. A NovaOryn SDK update may refresh it. The project creator preserves the user-owned `Kernel\Kernel.cs` file.
