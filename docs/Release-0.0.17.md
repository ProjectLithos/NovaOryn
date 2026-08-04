# Nova Oryn OS SDK 0.0.17

## Purpose

Version 0.0.17 corrects the first NativeAOT link boundary discovered by the Windows x64 build.

The stock `win-x64` NativeAOT runtime pack contains dormant Windows platform paths. Even after ILC trims the reachable kernel graph, its static archive can retain unresolved Win32 imports. NovaOryn must not link Windows DLL import libraries into a UEFI application.

## Changes

- disabled complete NativeAOT type metadata for the kernel
- disabled reflection, stack-trace data, EventSource, debugger support, metadata updates, and built-in COM interop
- removed `Marshal.PtrToStructure` from the native-to-managed entry bridge
- added a diagnostic first LLD link pass with unlimited error reporting
- separates unresolved NovaOryn/runtime-contract symbols from host-platform imports
- refuses to hide missing NovaOryn or NativeAOT runtime-contract symbols
- generates a freestanding compatibility object only for isolated host-platform imports
- records every compatibility import in `Artifacts/MinimalKernel/NovaOryn.NativeAot.PlatformImports.txt`
- performs a final LLD link with the generated compatibility object

## Important boundary

The generated compatibility functions exist only to make unreachable host-specific branches in the stock runtime pack linkable. They return failure or zero and are not a replacement for the planned NovaOryn freestanding CoreLib and runtime pack.

The next runtime milestone must replace the stock Windows CoreLib/runtime pack so no host-platform compatibility imports remain.
