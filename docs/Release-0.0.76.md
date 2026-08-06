# NovaOryn 0.0.76

NovaOryn 0.0.76 removes backup creation from generated-kernel migration.

Recognised SDK-generated monolithic `Kernel.cs` files are development artifacts and are now removed directly. Root-level generated kernels are deleted before template refresh, while generated nested kernels are overwritten by the clean high-level `Kernel\Kernel.cs`. Genuinely user-authored kernels remain protected.

Source-policy tests now reject both legacy backup suffixes and require direct removal of the obsolete root kernel.
