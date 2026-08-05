using System.Text.RegularExpressions;

string root = FindRepositoryRoot(AppContext.BaseDirectory);
List<string> failures = [];
foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
{
    string source = File.ReadAllText(file);
    if (source.Contains("public void ", StringComparison.Ordinal) || source.Contains("public static void ", StringComparison.Ordinal))
    {
        failures.Add($"Public void method found: {Path.GetRelativePath(root, file)}");
    }
}

string kernel = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Sample", "Kernel.cs"));
if (!Regex.IsMatch(kernel, @"public\s+static\s+bool\s+KMain\s*\("))
{
    failures.Add("KMain must be public static bool.");
}
string cpu = File.ReadAllText(Path.Combine(root, "native", "x64", "Cpu.S"));
if (!cpu.Contains("cli", StringComparison.Ordinal) || !cpu.Contains("hlt", StringComparison.Ordinal) || !cpu.Contains("jmp .LNovaOrynHaltForever", StringComparison.Ordinal))
{
    failures.Add("Native halt loop is incomplete.");
}

string x64CpuApi = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Architecture.X64", "CPU.cs"));
foreach (string requiredDocumentation in new[]
{
    "/// <summary>Provides direct x64 processor operations",
    "/// <summary>Enables maskable interrupts",
    "/// <summary>Disables maskable interrupts",
    "/// <summary>Determines whether maskable interrupts",
    "/// <summary>Enters the architecture-defined terminal halt",
    "/// <summary>Provides x64 port-mapped input/output operations",
    "/// <summary>Writes one byte to an x64 I/O port",
    "/// <summary>Attempts to read one byte from an x64 I/O port"
})
{
    if (!x64CpuApi.Contains(requiredDocumentation, StringComparison.Ordinal))
    {
        failures.Add($"The public x64 compatibility API is missing XML documentation: {requiredDocumentation}");
    }
}

string solution = File.ReadAllText(Path.Combine(root, "NovaOryn.sln"));
if (!solution.Contains("NovaOryn.Architecture.Arm64", StringComparison.Ordinal))
{
    failures.Add("The ARM64 architecture assembly must be included in the authoritative solution.");
}
if (!solution.Contains("Release|Any CPU", StringComparison.Ordinal))
{
    failures.Add("Solution must define Release|Any CPU.");
}
string buildScript = File.ReadAllText(Path.Combine(root, "Build-NovaOryn.ps1"));
if (!buildScript.Contains("--property:Platform=\"Any CPU\"", StringComparison.Ordinal))
{
    failures.Add("Build script must explicitly select Any CPU for managed solution projects.");
}
if (!buildScript.Contains("--ilc $ilc", StringComparison.Ordinal))
{
    failures.Add("Build script must pass the repository-pinned ILC executable to NovaOryn.ManagedCompiler.");
}
if (!buildScript.Contains("NovaOryn.SourcePolicy.Tests", StringComparison.Ordinal))
{
    failures.Add("Build script must execute the source-policy tests.");
}

string projectManifest = File.ReadAllText(Path.Combine(root, "examples", "MinimalKernel", "NovaOrynProject.json"));
if (!projectManifest.Contains("NovaOryn.Kernel.Bootstrap", StringComparison.Ordinal))
{
    failures.Add("Minimal kernel manifest must select the NovaOryn bootstrap system module.");
}
string bootstrapProject = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "NovaOryn.Kernel.Bootstrap.csproj"));
if (!bootstrapProject.Contains("<ImplicitUsings>disable</ImplicitUsings>", StringComparison.Ordinal))
{
    failures.Add("Freestanding bootstrap must disable generated global usings.");
}
if (!bootstrapProject.Contains("<Nullable>disable</Nullable>", StringComparison.Ordinal))
{
    failures.Add("Freestanding bootstrap must disable nullable metadata generation.");
}
foreach (string forbidden in new[] { "<PublishAot>", "<RuntimeIdentifier>", "<SelfContained>", "<NativeLib>" })
{
    if (bootstrapProject.Contains(forbidden, StringComparison.Ordinal))
    {
        failures.Add($"Freestanding bootstrap project must compile to IL only and must not contain {forbidden}.");
    }
}

string bootstrapCoreLib = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "CoreLib.cs"));
if (!bootstrapCoreLib.Contains("#pragma warning disable CS0169", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("private IntPtr _methodTable;", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("#pragma warning restore CS0169", StringComparison.Ordinal))
{
    failures.Add("Freestanding Object must retain its NativeAOT method-table field with a narrowly scoped CS0169 suppression.");
}
if (!bootstrapCoreLib.Contains("internal class Array<T> : Array", StringComparison.Ordinal))
{
    failures.Add("Freestanding CoreLib must provide the compiler-required generic array type.");
}
if (!bootstrapCoreLib.Contains("public static class Buffer", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("BulkMoveWithWriteBarrier", StringComparison.Ordinal))
{
    failures.Add("Freestanding CoreLib must provide the .NET 10 ILC System.Buffer helper contract.");
}

string managedCompiler = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ManagedCompiler", "Program.cs"));
if (managedCompiler.Contains("\"publish\"", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.ManagedCompiler must not use dotnet publish for the no-CoreLib bootstrap.");
}
if (managedCompiler.Contains("\"-O\"", StringComparison.Ordinal))
{
    failures.Add("No-GC bootstrap ILC compilation must not enable -O because it implies the IL scanner.");
}
foreach (string required in new[] { "--systemmodule", "--targetos:win", "--targetarch:x64", "--nativelib", "--directpinvoke:*", "--noscan", "--reflectiondata:none", "--nopreinitstatics" })
{
    if (!managedCompiler.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Direct ILC invocation is missing {required}.");
    }
}
if (!managedCompiler.Contains("nativeObject", StringComparison.Ordinal))
{
    failures.Add("Compilation manifest must record the direct ILC native object.");
}
if (!managedCompiler.Contains("schemaVersion = 5", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.ManagedCompiler must emit compilation manifest schema 5.");
}

string linker = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Linker", "Program.cs"));
if (!linker.Contains("GetProperty(\"nativeObject\")", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.Linker must consume the direct ILC native object.");
}
if (linker.Contains("nativeLibrary", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.Linker must not expect the obsolete NativeAOT static library output.");
}
if (!linker.Contains("SupportedCompilationManifestSchema = 5", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.Linker must accept compilation manifest schema 5.");
}


string bootstrapKernel = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "Kernel.cs"));
if (!bootstrapKernel.Contains("WriteLineNovaOrynStarted", StringComparison.Ordinal) ||
    !bootstrapKernel.Contains("WriteLineCpuHalted", StringComparison.Ordinal))
{
    failures.Add("Freestanding bootstrap must emit both runtime acceptance lines before halting.");
}

string uefiEntry = File.ReadAllText(Path.Combine(root, "native", "x64", "Entry.asm"));
foreach (string required in new[]
{
    "NovaOrynGraphicsOutputProtocolGuid",
    "NovaOrynCaptureUefiFramebuffer",
    "[rcx + 0x60]",
    "[rax + 0x140]",
    "NovaOrynBootContext + 0x08",
    "NovaOrynBootContext + 0x24",
    "lea rcx, [rel NovaOrynBootContext]"
})
{
    if (!uefiEntry.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"x64 UEFI entry is missing GOP boot-context capture detail: {required}");
    }
}
int captureCall = uefiEntry.IndexOf("call NovaOrynCaptureUefiFramebuffer", StringComparison.Ordinal);
int interruptsDisabled = uefiEntry.IndexOf("    cli", StringComparison.Ordinal);
if (captureCall < 0 || interruptsDisabled < captureCall)
{
    failures.Add("UEFI GOP discovery must complete before interrupts are disabled.");
}

string bootstrapBootContext = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "BootContext.cs"));
foreach (string required in new[] { "FramebufferAddress", "FramebufferSize", "PixelsPerScanLine", "PixelFormat", "RedMask", "GreenMask", "BlueMask" })
{
    if (!bootstrapBootContext.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Freestanding boot context is missing framebuffer field: {required}");
    }
}

string bootstrapFramebuffer = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "FramebufferConsole.cs"));
foreach (string required in new[]
{
    "context->PixelsPerScanLine < context->Width",
    "context->FramebufferSize / bytesPerScanLine",
    "context->PixelFormat > 2U",
    "internal Boolean Clear()",
    "BitmapFont.GetGlyph",
    "PackColor",
    "EncodeMask"
})
{
    if (!bootstrapFramebuffer.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Managed framebuffer bootstrap is missing validation/rendering contract: {required}");
    }
}
if (!bootstrapKernel.Contains("framebuffer.Initialize(boot)", StringComparison.Ordinal) ||
    !bootstrapKernel.Contains("framebuffer.Clear()", StringComparison.Ordinal) ||
    !bootstrapKernel.Contains("Native.WritePort8(0x3F8, value)", StringComparison.Ordinal) ||
    !bootstrapKernel.Contains("framebuffer.Write(value)", StringComparison.Ordinal))
{
    failures.Add("KMain must initialize and clear the framebuffer and mirror each serial character to it.");
}

string framebufferProject = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Console.Framebuffer", "NovaOryn.Console.Framebuffer.csproj"));
string framebufferAssembly = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Console.Framebuffer", "FramebufferConsole.cs"));
if (!framebufferProject.Contains("<AssemblyName>NovaOryn.Console.Framebuffer</AssemblyName>", StringComparison.Ordinal) ||
    !framebufferAssembly.Contains("public sealed unsafe class FramebufferConsole : IConsole", StringComparison.Ordinal))
{
    failures.Add("The reusable managed framebuffer console assembly is not present.");
}
if (!solution.Contains("NovaOryn.Console.Framebuffer", StringComparison.Ordinal) ||
    !kernel.Contains("FramebufferConsole", StringComparison.Ordinal) ||
    !kernel.Contains("WriteLine(serial, framebuffer", StringComparison.Ordinal))
{
    failures.Add("The solution and kernel sample must demonstrate serial/framebuffer mirroring.");
}

string imageBuilder = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ImageBuilder", "Program.cs"));
string efiDiskImage = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ImageBuilder", "EfiDiskImage.cs"));
foreach (string required in new[] { "BOOTX64.EFI", "GPT/FAT32 EFI System Partition", "NovaOryn.Image.json" })
{
    if (!imageBuilder.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"NovaOryn.ImageBuilder is missing boot-image contract: {required}");
    }
}
foreach (string required in new[] { "EFI PART", "FAT32", "BOOTX64 EFI", "EfiSystemPartitionType", "WriteGuidPartitionTable" })
{
    if (!efiDiskImage.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"NovaOryn.ImageBuilder is missing real GPT/FAT32 implementation detail: {required}");
    }
}
if (imageBuilder.Contains("FoundationOnly", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.ImageBuilder must not remain a foundation-only placeholder.");
}

string qemuLauncher = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.QemuLauncher", "Program.cs"));
foreach (string required in new[] { "edk2-x86_64-code.fd", "edk2-i386-vars.fd", "NovaOryn KMain started.", "CPU halted.", "qemuRemainedOpen = true", "-no-shutdown" })
{
    if (!qemuLauncher.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"NovaOryn.QemuLauncher is missing runtime acceptance contract: {required}");
    }
}
if (qemuLauncher.Contains("\"-S\"", StringComparison.Ordinal) || qemuLauncher.Contains("process.WaitForExit()", StringComparison.Ordinal))
{
    failures.Add("QEMU runtime acceptance must launch immediately and return while the halted VM remains open.");
}

string installer = File.ReadAllText(Path.Combine(root, "Install-NovaOrynToolchain.ps1"));
foreach (string required in new[] { "Ensure-Ovmf", "ovmfCodeX64", "ovmfVarsX64", "edk2-x86_64-code.fd", "edk2-i386-vars.fd" })
{
    if (!installer.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Toolchain installer is missing x64 OVMF handling: {required}");
    }
}

if (!buildScript.Contains("NovaOryn.ImageBuilder", StringComparison.Ordinal) ||
    !buildScript.Contains("NovaOryn.QemuLauncher", StringComparison.Ordinal) ||
    !buildScript.Contains("--ovmf-code", StringComparison.Ordinal) ||
    !buildScript.Contains("--ovmf-vars", StringComparison.Ordinal))
{
    failures.Add("Build script must create the FAT32 image and execute OVMF/QEMU runtime acceptance.");
}

string projectCreator = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ProjectCreator", "Program.cs"));
if (!projectCreator.Contains("Source", StringComparison.Ordinal) || !projectCreator.Contains("Repos", StringComparison.Ordinal) || !projectCreator.Contains("NovaOrynKernel", StringComparison.Ordinal))
{
    failures.Add("Project creator must default to the user Source\\Repos\\NovaOrynKernel directory.");
}
if (!buildScript.Contains("Source\\Repos\\NovaOrynKernel", StringComparison.Ordinal) || !buildScript.Contains("NovaOryn.ProjectCreator", StringComparison.Ordinal))
{
    failures.Add("Build script must create and consume the external C# kernel project.");
}
string kernelTemplate = Path.Combine(root, "templates", "NovaOrynKernel", "Kernel", "Kernel.cs");
if (!File.Exists(kernelTemplate))
{
    failures.Add("External C# kernel project template is missing Kernel.cs.");
}

if (failures.Count != 0)
{
    foreach (string failure in failures)
    {
        Console.Error.WriteLine($"[FAIL] {failure}");
    }
    return 1;
}
Console.WriteLine("[ OK ] Public C# APIs contain no public void methods.");
Console.WriteLine("[ OK ] Kernel entry is KMain and returns bool.");
Console.WriteLine("[ OK ] x64 halt executes CLI and a repeating HLT loop.");
Console.WriteLine("[ OK ] No-CoreLib kernel compilation invokes ILC directly.");
Console.WriteLine("[ OK ] Windows NativeAOT runtime-pack resolution is not used.");
Console.WriteLine("[ OK ] UEFI GOP capture and managed framebuffer rendering are wired.");
Console.WriteLine("[ OK ] GPT/FAT32 image creation and OVMF/QEMU runtime acceptance are wired.");
return 0;

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("NovaOryn repository root was not found.");
}
