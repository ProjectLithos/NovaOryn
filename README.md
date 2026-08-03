# Nova Oryn OS SDK

Nova Oryn OS SDK (`NovaOryn`) is a from-scratch SDK for compiling user-owned freestanding C# kernels and operating systems with the real .NET NativeAOT compiler (`ilc`).

## Release 0.0.5

The first source release established the repository, public API rules, `KMain` kernel-entry contract, x64 native entry and halt assets, and the executable-oriented compiler/linker/image/QEMU tool boundaries.

This release is an implementation foundation. It does not yet claim a boot-complete NativeAOT runtime.

## First repository commit

The first Git commit must be created from the **FullSource** archive, not the ChangedFiles archive.

1. Create or empty `C:\NovaOryn`.
2. Extract `NovaOryn-FullSource-0.0.5.zip` directly into `C:\NovaOryn`.
3. Initialise Git and connect the repository to:

```text
https://github.com/ProjectLithos/NovaOryn.git
```

4. Commit and push the complete source tree.
5. Only after that commit has been pushed may the pinned toolchain be downloaded into `C:\NovaOryn\.toolchain`.

The ChangedFiles archive is included to preserve the standard two-archive release format. Because version `0.0.1` starts from an empty repository, it contains the same source-controlled files as FullSource, but it is not the archive used for the initial commit.

## Later releases

For version `0.0.5` and later:

- extract `NovaOryn-ChangedFiles-<version>.zip` into the existing `C:\NovaOryn` repository
- review, commit, and push those source changes
- run any explicitly requested toolchain update only after the source commit
- use `NovaOryn-FullSource-<version>.zip` as the complete authoritative snapshot

### Empty repository handling

Version 0.0.5 checks for `HEAD` without allowing Git's expected empty-repository diagnostic to terminate PowerShell. A repository that has been initialised but has no commits is therefore correctly treated as requiring FullSource.

## Build policy

Kernel and OS creation is performed by NovaOryn executable tools. No script translates, creates, links, or packages the kernel.

## Entry point

```csharp
[KernelEntry]
public static bool KMain(BootContext boot)
```

## Projects in 0.0.5

- `NovaOryn.Primitives`
- `NovaOryn.Core`
- `NovaOryn.Runtime.Contracts`
- `NovaOryn.Boot.Contracts`
- `NovaOryn.Architecture.Contracts`
- `NovaOryn.Architecture.X64`
- `NovaOryn.Console.Contracts`
- `NovaOryn.Console.Serial`
- `NovaOryn.ProjectModel`
- `NovaOryn.ManagedCompiler`
- `NovaOryn.Linker`
- `NovaOryn.ImageBuilder`
- `NovaOryn.QemuLauncher`
- `NovaOryn.Kernel.Sample`
- `NovaOryn.SourcePolicy.Tests`

See `docs/Release-0.0.5.md` for the current release and `docs/Release-0.0.1.md` for the initial foundation.


## Source archive updater

`Update-NovaOryn.bat` automates source archive selection and Git commits.

- When `C:\NovaOryn` has no commit, it selects the highest-versioned `NovaOryn-FullSource-x.y.z.zip`, extracts the complete tree, and creates the initial commit.
- After the first commit, it selects the highest-versioned `NovaOryn-ChangedFiles-x.y.z.zip`, applies deletions and renames from `NovaOryn-Changes.json`, stages only the resulting differences, and creates the update commit.
- It searches the batch-file directory and the current user's Downloads directory by default. A different archive directory may be passed as the first argument.
- It refuses to overwrite an existing repository with uncommitted changes.
- It does not push and does not download the toolchain. The commit must be reviewed and pushed before the separate toolchain installer is run.

Example:

```text
Update-NovaOryn.bat
Update-NovaOryn.bat D:\NovaOryn-Releases
```
