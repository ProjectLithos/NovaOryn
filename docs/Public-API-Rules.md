# Public API rules

1. Public callable members return `bool` or a meaningful value.
2. Public `void` methods are forbidden.
3. Expected operational failures return `false`.
4. Fallible queries use `Try...` and an `out` value.
5. Invalid API use may throw normal .NET exceptions after runtime initialization.
6. Fatal kernel failures panic and halt.
7. Fallible public constructors and state-changing public property setters are avoided.
8. `CPU.Halt()` returns `bool` by signature but never returns after success.
