# NovaOryn 0.0.84

NovaOryn 0.0.84 corrects the source-policy test runner so updater regression checks use the existing failure-collection mechanism. The three updater checks are evaluated before the final failure report and no longer call a nonexistent `Assert` helper.

No kernel, NativeAOT, linker, descriptor, interrupt, console, or project-migration behaviour changes in this release.
