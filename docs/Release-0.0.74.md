# NovaOryn 0.0.74

NovaOryn 0.0.74 corrects external kernel migration for the historical root-level monolithic `Kernel.cs`.

The recognizer no longer depends on one narrow combination of method names. It identifies the SDK-generated low-level kernel using stable structural markers: native interop declarations, NovaOryn x64 entry points, serial port output, framebuffer console code, the managed runtime export, and `KMain`. The original file is backed up as `Kernel.cs.pre-0.0.74.bak`, removed from the project root, and replaced by the high-level `Kernel\Kernel.cs`.

A user-authored root kernel that does not contain the generated low-level structure remains protected and causes an explicit migration failure rather than being overwritten.
