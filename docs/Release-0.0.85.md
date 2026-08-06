# NovaOryn 0.0.85

NovaOryn 0.0.85 fixes the external-kernel validation step in `Build-NovaOryn.ps1` for Windows PowerShell 5.1. The build no longer calls the unavailable two-argument `String.Contains` overload and instead uses an ordinal `String.IndexOf` comparison.

The external project refresh and high-level-only `Kernel\Kernel.cs` validation remain unchanged. A source-policy regression check prevents the incompatible overload from returning.
