# NovaOryn 0.0.75

NovaOryn 0.0.75 corrects the source-policy regression that prevented the external kernel migration from running.

The project creator already backs up recognised SDK-generated root-level monolithic kernels as `Kernel.cs.pre-0.0.74.bak`. The policy test incorrectly required the obsolete `.pre-0.0.69.bak` suffix, causing a false failure before project refresh. The test now validates the backup suffix actually used by the migration implementation.

No kernel architecture or public API behaviour changes in this release.
