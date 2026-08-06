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

string samplePlatform = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Sample", "PlatformInitialization.cs"));
foreach (string required in new[]
{
    "X64TaskStateSegment",
    "X64GlobalDescriptorTable",
    "X64InterruptDescriptorTable",
    "EssentialExceptionHandlers",
    "X64InterruptController",
    "InterruptDeliveryMechanism.Msi",
    "CreateMessage",
    "SetAffinity",
    "SetPriority",
    "Unmask",
    "Mask",
    "RemoveRoute",
    "ReleaseVector"
})
{
    if (!samplePlatform.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"The sample kernel does not demonstrate public platform facility: {required}");
    }
}
if (!kernel.Contains("PlatformInitialization.Initialize", StringComparison.Ordinal) ||
    !kernel.Contains("KernelDiagnosticSink", StringComparison.Ordinal))
{
    failures.Add("KMain must initialise the public descriptor, interrupt, exception, and controller facilities.");
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
if (!buildScript.Contains("External user kernel is high-level only", StringComparison.Ordinal) ||
    !buildScript.Contains("External kernel migration left an obsolete root Kernel.cs", StringComparison.Ordinal))
{
    failures.Add("The build must prove that external generated kernels no longer expose the obsolete root Kernel.cs.");
}
if (!buildScript.Contains("--property:Platform=\"Any CPU\"", StringComparison.Ordinal))
{
    failures.Add("Build script must explicitly select Any CPU for managed solution projects.");
}
if (buildScript.Contains(".Contains($forbiddenKernelToken, [StringComparison]::Ordinal)", StringComparison.Ordinal) ||
    !buildScript.Contains(".IndexOf($forbiddenKernelToken, [StringComparison]::Ordinal) -ge 0", StringComparison.Ordinal))
{
    failures.Add("Build script must use a Windows PowerShell-compatible ordinal kernel-token check.");
}
if (!buildScript.Contains("--ilc $ilc", StringComparison.Ordinal))
{
    failures.Add("Build script must pass the repository-pinned ILC executable to NovaOryn.ManagedCompiler.");
}
if (!buildScript.Contains("NovaOryn.SourcePolicy.Tests", StringComparison.Ordinal))
{
    failures.Add("Build script must execute the source-policy tests.");
}

string memoryContracts = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Memory.Contracts", "MemoryDescriptor.cs"));
string memoryEnums = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Memory.Contracts", "MemoryEnums.cs"));
string memoryMapContracts = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Memory.Contracts", "MemoryMapContracts.cs"));
string memoryNormalisers = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Boot.Memory", "MemoryMapNormalisers.cs"));
string finalUefiMap = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Boot.Memory", "FinalUefiMemoryMap.cs"));
string nativeMemorySource = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Boot.Memory", "NativeUefiMemoryMapSource.cs"));
string reservationPlan = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Boot.Memory", "MemoryReservationPlan.cs"));
string nativeEntry = File.ReadAllText(Path.Combine(root, "native", "x64", "Entry.asm"));
foreach (string requiredMemoryType in new[]
{
    "UsableConventional", "LoaderKernelImage", "BootServices", "RuntimeServices",
    "AcpiReclaimable", "AcpiNvs", "Framebuffer", "MemoryMappedIo",
    "FirmwareReserved", "BadMemory", "PersistentMemory", "BootStructures",
    "PageTables", "EarlyAllocatorAllocations"
})
{
    if (!memoryEnums.Contains(requiredMemoryType, StringComparison.Ordinal))
        failures.Add($"Memory contracts are missing required type: {requiredMemoryType}");
}
if (!memoryContracts.Contains("pageCount > ulong.MaxValue / 4096UL", StringComparison.Ordinal) ||
    !memoryContracts.Contains("physicalStart.Value > ulong.MaxValue - length", StringComparison.Ordinal))
{
    failures.Add("Memory descriptors must reject page-length and end-address overflow.");
}
foreach (string requiredNormaliser in new[] { "StrictMemoryMapNormaliser", "SafetyPriorityMemoryMapNormaliser", "ConservativeMemoryMapNormaliser" })
{
    if (!memoryNormalisers.Contains(requiredNormaliser, StringComparison.Ordinal))
        failures.Add($"Boot memory assembly is missing normaliser implementation: {requiredNormaliser}");
}
if (!memoryNormalisers.Contains("SortAndDeduplicate", StringComparison.Ordinal) ||
    !memoryNormalisers.Contains("TrySlice", StringComparison.Ordinal) ||
    !memoryNormalisers.Contains("IsMergeCompatible", StringComparison.Ordinal))
{
    failures.Add("Memory normalisation must sort, split, and merge compatible adjacent ranges.");
}
if (!memoryMapContracts.Contains("CreateDiagnosticCursor", StringComparison.Ordinal) ||
    !memoryMapContracts.Contains("private readonly MemoryDescriptor[] _descriptors", StringComparison.Ordinal))
{
    failures.Add("Normalised memory maps must expose immutable diagnostic enumeration.");
}
if (!finalUefiMap.Contains("provider.GetMemoryMap", StringComparison.Ordinal) ||
    !finalUefiMap.Contains("provider.ExitBootServices", StringComparison.Ordinal) ||
    !finalUefiMap.Contains("InvalidMapKey", StringComparison.Ordinal))
{
    failures.Add("Final UEFI map acquisition must retry stale map keys and seal only the accepted map.");
}
int nativeGetMemoryMapCall = nativeEntry.IndexOf("call rdi", StringComparison.Ordinal);
int nativeExitBootServicesCall = nativeEntry.IndexOf("call r12", StringComparison.Ordinal);
if (!nativeEntry.Contains("NovaOrynCaptureFinalUefiMemoryMap", StringComparison.Ordinal) ||
    !nativeEntry.Contains("No allocation or firmware operation occurs", StringComparison.Ordinal) ||
    nativeGetMemoryMapCall < 0 || nativeExitBootServicesCall < 0 ||
    nativeGetMemoryMapCall > nativeExitBootServicesCall)
{
    failures.Add("Native UEFI entry must obtain the final map immediately before ExitBootServices.");
}
if (!nativeEntry.Contains("div qword [rel NovaOrynBootContext + 0x50]", StringComparison.Ordinal) ||
    !nativeEntry.Contains("test qword [rel NovaOrynBootContext + 0x50], 7", StringComparison.Ordinal))
{
    failures.Add("Native UEFI entry must validate descriptor alignment and complete map records before ExitBootServices.");
}
if (!nativeMemorySource.Contains("boot.MemoryMapLength % boot.MemoryDescriptorSize", StringComparison.Ordinal) ||
    !nativeMemorySource.Contains("TryGetUefiDescriptor", StringComparison.Ordinal))
{
    failures.Add("Boot memory must expose a checked immutable adapter over the retained native UEFI map.");
}
if (!reservationPlan.Contains("TryValidateRequiredReservations", StringComparison.Ordinal) ||
    !reservationPlan.Contains("HasKernelImage", StringComparison.Ordinal) ||
    !reservationPlan.Contains("HasBootStructures", StringComparison.Ordinal) ||
    !memoryNormalisers.Contains("TryCreateReservationInterval", StringComparison.Ordinal) ||
    !memoryNormalisers.Contains("runtimeStatus == MemoryRuntimeStatus.NotRuntime ? reservation.Availability : MemoryAvailability.RuntimeOwned", StringComparison.Ordinal))
{
    failures.Add("Reservation planning must validate mandatory categories and overlays must preserve firmware runtime ownership.");
}
if (!solution.Contains("NovaOryn.Memory.Contracts", StringComparison.Ordinal) ||
    !solution.Contains("NovaOryn.Boot.Memory", StringComparison.Ordinal) ||
    !solution.Contains("NovaOryn.Memory.Tests", StringComparison.Ordinal))
{
    failures.Add("The authoritative solution must include memory contracts, boot memory, and memory tests.");
}
if (!buildScript.Contains("NovaOryn.Memory.Tests", StringComparison.Ordinal))
{
    failures.Add("Build script must execute boot-memory tests.");
}


string sampleKernelProject = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Sample", "NovaOryn.Kernel.Sample.csproj"));
if (!sampleKernelProject.Contains("NovaOryn.Boot.Memory", StringComparison.Ordinal) ||
    !kernel.Contains("NativeUefiMemoryMapSource.TryCreate", StringComparison.Ordinal))
{
    failures.Add("The sample kernel must demonstrate the retained native UEFI memory-map adapter.");
}
string bootstrapBootContext = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "BootContext.cs"));
string standaloneTemplateBootContext = File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "BootContext.cs"));
string visualStudioTemplateBootContext = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "BootContext.cs"));
foreach (string requiredBootContextToken in new[] { "FinalMemoryMapAddress", "FinalMemoryDescriptorSize", "HasFinalMemoryMap" })
{
    if (!bootstrapBootContext.Contains(requiredBootContextToken, StringComparison.Ordinal) ||
        !standaloneTemplateBootContext.Contains(requiredBootContextToken, StringComparison.Ordinal) ||
        !visualStudioTemplateBootContext.Contains(requiredBootContextToken, StringComparison.Ordinal))
    {
        failures.Add($"All freestanding boot-context copies must expose final-map token: {requiredBootContextToken}");
    }
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

string bootstrapCoreLib = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Freestanding.CoreLib", "CoreLib.cs"));
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
if (!bootstrapCoreLib.Contains("namespace Reflection", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("class DefaultMemberAttribute", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("DefaultMemberAttribute(String memberName)", StringComparison.Ordinal))
{
    failures.Add("Freestanding CoreLib must provide the compiler-required System.Reflection.DefaultMemberAttribute constructor used by indexed members.");
}
string templateCoreLib = File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Sdk", "NovaOryn.Freestanding.CoreLib", "CoreLib.cs"));
if (!templateCoreLib.Contains("class DefaultMemberAttribute", StringComparison.Ordinal) ||
    !templateCoreLib.Contains("DefaultMemberAttribute(String memberName)", StringComparison.Ordinal))
{
    failures.Add("The kernel template CoreLib must include System.Reflection.DefaultMemberAttribute.");
}

string bootstrapConsole = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!bootstrapCoreLib.Contains("private readonly Int32 _stringLength;", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("private Char _firstChar;", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("fixed (Char* firstCharacter = &_firstChar)", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("return firstCharacter[index];", StringComparison.Ordinal))
{
    failures.Add("Freestanding System.String must expose its NativeAOT inline character layout and a terminating character indexer.");
}
if (!bootstrapCoreLib.Contains("#pragma warning disable CS0649", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("#pragma warning restore CS0649", StringComparison.Ordinal))
{
    failures.Add("Freestanding System.String runtime layout fields must use a narrowly scoped CS0649 suppression because NativeAOT, not C# constructors, populates them.");
}
if (bootstrapCoreLib.Contains("public sealed class String { public readonly Int32 Length; public Char this[Int32 index] { get { while (true)", StringComparison.Ordinal))
{
    failures.Add("Freestanding System.String must not retain the non-terminating placeholder indexer.");
}
if (!bootstrapConsole.Contains("public static Boolean Write(String value)", StringComparison.Ordinal) ||
    !bootstrapConsole.Contains("public static UInt32 FontSize", StringComparison.Ordinal) ||
    !bootstrapConsole.Contains("Initialize(BootContext boot, UInt32 fontSize)", StringComparison.Ordinal) ||
    !bootstrapConsole.Contains("_framebuffer.Initialize(boot, fontSize)", StringComparison.Ordinal) ||
    !bootstrapConsole.Contains("Char character = value[index];", StringComparison.Ordinal) ||
    bootstrapConsole.Contains("public static unsafe Boolean Write(String value)", StringComparison.Ordinal) ||
    bootstrapConsole.Contains("fixed (Char* characters = value)", StringComparison.Ordinal))
{
    failures.Add("KernelConsole.Write must remain a normal managed C# method and use the terminating freestanding string indexer.");
}
string visualStudioCoreLib = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel", "Sdk", "NovaOryn.Freestanding.CoreLib", "CoreLib.cs"));
string visualStudioConsole = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!string.Equals(bootstrapCoreLib, templateCoreLib, StringComparison.Ordinal) ||
    !string.Equals(bootstrapCoreLib, visualStudioCoreLib, StringComparison.Ordinal) ||
    !string.Equals(bootstrapConsole, File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "KernelConsole.cs")), StringComparison.Ordinal) ||
    !string.Equals(bootstrapConsole, visualStudioConsole, StringComparison.Ordinal))
{
    failures.Add("Authoritative, command-line, and Visual Studio freestanding string/console implementations must remain identical.");
}

string commandLineUserKernel = File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Kernel", "Kernel.cs"));
string visualStudioUserKernel = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel", "Kernel", "Kernel.cs"));
if (!commandLineUserKernel.Contains("KernelConsole.Initialize(boot, 32U)", StringComparison.Ordinal) ||
    !visualStudioUserKernel.Contains("KernelConsole.Initialize(boot, 32U)", StringComparison.Ordinal))
{
    failures.Add("Generated kernels must pass the exact 32-pixel font size used by the framebuffer renderer.");
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
if (!managedCompiler.Contains("GetFiles(managedOutput, \"*.dll\"", StringComparison.Ordinal) ||
    !managedCompiler.Contains("managedInputs", StringComparison.Ordinal) ||
    !managedCompiler.Contains("systemModuleAssembly", StringComparison.Ordinal))
{
    failures.Add("Direct ILC compilation must include every managed bootstrap assembly and verify the configured system module exists.");
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
if (!bootstrapKernel.Contains("KernelPlatform.InitializeDescriptors", StringComparison.Ordinal) ||
    !bootstrapKernel.Contains("KernelPlatform.InitializeInterrupts", StringComparison.Ordinal) ||
    !bootstrapKernel.Contains("KernelPlatform.DisableLegacyPic", StringComparison.Ordinal))
{
    failures.Add("The actual ILC bootstrap must install GDT/TSS, IDT, and disable the legacy PIC through the high-level platform assembly.");
}
foreach (string message in new[] { "NovaOryn KMain started.", "GDT and TSS installed.", "IDT with 256 vectors installed.", "CPU halted." })
{
    if (!bootstrapKernel.Contains($"KernelConsole.WriteLine(\"{message}\")", StringComparison.Ordinal))
    {
        failures.Add($"The booting kernel must visibly report: {message}");
    }
}
if (bootstrapKernel.Contains("DllImport", StringComparison.Ordinal) || bootstrapKernel.Contains("WritePort8", StringComparison.Ordinal) || bootstrapKernel.Contains("class Native", StringComparison.Ordinal))
{
    failures.Add("The end-user kernel source must not expose native imports or low-level port I/O.");
}
if (bootstrapKernel.Contains("RuntimeExport", StringComparison.Ordinal) || bootstrapKernel.Contains("NativeEntry", StringComparison.Ordinal))
{
    failures.Add("The end-user kernel source must not expose the native runtime entry bridge.");
}
string kernelEntry = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Entry.X64", "KernelEntry.cs"));
if (!kernelEntry.Contains("RuntimeExport(\"NovaOrynManagedEntry\")", StringComparison.Ordinal) ||
    !kernelEntry.Contains("global::NovaOryn.Kernel.Bootstrap.Kernel.KMain(new BootContext(bootContextAddress))", StringComparison.Ordinal))
{
    failures.Add("The separate x64 entry assembly must own the runtime export and dispatch to KMain.");
}
string bootstrapManifest = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "NovaOrynProject.json"));
if (!bootstrapManifest.Contains("NovaOryn.Kernel.Entry.X64.csproj", StringComparison.Ordinal))
{
    failures.Add("The authoritative bootstrap manifest must compile through the separate entry assembly.");
}
string projectCreator = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ProjectCreator", "Program.cs"));
if (!projectCreator.Contains("IsSdkGeneratedLowLevelKernel", StringComparison.Ordinal) ||
    projectCreator.Contains(".pre-0.0.69.bak", StringComparison.Ordinal) ||
    projectCreator.Contains(".pre-0.0.74.bak", StringComparison.Ordinal))
{
    failures.Add("Project creation must replace generated low-level Kernel.cs files without creating backups.");
}
if (!projectCreator.Contains("MigrateLegacyRootKernel", StringComparison.Ordinal) ||
    !projectCreator.Contains("Path.Combine(output, \"Kernel.cs\")", StringComparison.Ordinal) ||
    projectCreator.Contains("legacyRootKernel + \".pre-\"", StringComparison.Ordinal) ||
    !projectCreator.Contains("File.Delete(legacyRootKernel)", StringComparison.Ordinal))
{
    failures.Add("Project creation must migrate and remove the obsolete root-level generated Kernel.cs.");
}
if (!projectCreator.Contains("ResolveMainProjectPath", StringComparison.Ordinal) ||
    !projectCreator.Contains("Path.GetFileName(mainProjectPath)", StringComparison.Ordinal) ||
    !projectCreator.Contains("RemoveSdkOwnedLegacyTrees", StringComparison.Ordinal) ||
    !projectCreator.Contains("Directory.Delete(path, true)", StringComparison.Ordinal))
{
    failures.Add("Project refresh must preserve the selected root project filename and replace SDK-owned monolithic support trees.");
}
if (!projectCreator.Contains("source.Contains(\"DllImport\"", StringComparison.Ordinal) ||
    !projectCreator.Contains("source.Contains(\"WritePort8\"", StringComparison.Ordinal) ||
    !projectCreator.Contains("source.Contains(\"FramebufferConsole\"", StringComparison.Ordinal) ||
    !projectCreator.Contains("source.Contains(\"NovaOrynManagedEntry\"", StringComparison.Ordinal))
{
    failures.Add("Legacy kernel migration must recognize the generated monolithic low-level kernel by stable structural markers.");
}
string lowLevelAssembly = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.X64.LowLevel", "Native.cs"));
foreach (string nativeMember in new[] { "class Native", "InitializeSerial", "WriteSerial", "private static extern Boolean WritePort8", "InitializeBootstrapDescriptors", "InitializeBootstrapInterrupts", "DisableLegacyPic", "Halt" })
{
    if (!lowLevelAssembly.Contains(nativeMember, StringComparison.Ordinal)) failures.Add($"The low-level x64 assembly is missing {nativeMember}.");
}
string managedKernelConsole = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!managedKernelConsole.Contains("public static Boolean Write(String value)", StringComparison.Ordinal) ||
    !managedKernelConsole.Contains("public static Boolean WriteLine(String value)", StringComparison.Ordinal) ||
    managedKernelConsole.Contains("unsafe Boolean Write", StringComparison.Ordinal) ||
    managedKernelConsole.Contains("WritePort8", StringComparison.Ordinal) ||
    managedKernelConsole.Contains("0x3F8", StringComparison.Ordinal) ||
    !managedKernelConsole.Contains("Native.InitializeSerial()", StringComparison.Ordinal) ||
    !managedKernelConsole.Contains("Native.WriteSerial(value)", StringComparison.Ordinal))
{
    failures.Add("Freestanding Write and WriteLine must be normal managed C# functions with raw serial I/O confined to the low-level assembly.");
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

string framebufferBootContext = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "BootContext.cs"));
foreach (string required in new[] { "FramebufferAddress", "FramebufferSize", "PixelsPerScanLine", "PixelFormat", "RedMask", "GreenMask", "BlueMask" })
{
    if (!framebufferBootContext.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Freestanding boot context is missing framebuffer field: {required}");
    }
}

string bootstrapFramebuffer = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "FramebufferConsole.cs"));
foreach (string required in new[]
{
    "context->PixelsPerScanLine < context->Width",
    "context->FramebufferSize / bytesPerScanLine",
    "context->PixelFormat > 2U",
    "internal Boolean Clear()",
    "BitmapFont.GetGlyphRow",
    "BitmapFont.GlyphWidth",
    "BitmapFont.GetRenderedGlyphWidth",
    "BitmapFont.GetRenderedCharacterAdvance",
    "BitmapFont.GetRenderedLineHeight",
    "BitmapFont.GetSourceRow",
    "BitmapFont.GetSourceColumn",
    "_fontSize",
    "Initialize(BootContext boot, UInt32 fontSize)",
    "fontSize < BitmapFont.MinimumFontSize",
    "PackColor",
    "EncodeMask"
})
{
    if (!bootstrapFramebuffer.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Managed framebuffer bootstrap is missing validation/rendering contract: {required}");
    }
}
string bootstrapBitmapFont = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "BitmapFont.cs"));
string commandLineBitmapFont = File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "BitmapFont.cs"));
string visualStudioBitmapFont = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "BitmapFont.cs"));
string reusableBitmapFont = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Console.Framebuffer", "BitmapFont.cs"));
string commandLineFramebuffer = File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "FramebufferConsole.cs"));
string visualStudioFramebuffer = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Console", "FramebufferConsole.cs"));
if (!string.Equals(bootstrapFramebuffer, commandLineFramebuffer, StringComparison.Ordinal) ||
    !string.Equals(bootstrapFramebuffer, visualStudioFramebuffer, StringComparison.Ordinal))
{
    failures.Add("Authoritative, command-line, and Visual Studio templates must contain the identical freestanding framebuffer renderer.");
}
if (!string.Equals(bootstrapBitmapFont, commandLineBitmapFont, StringComparison.Ordinal) ||
    !string.Equals(bootstrapBitmapFont, visualStudioBitmapFont, StringComparison.Ordinal))
{
    failures.Add("Authoritative, command-line, and Visual Studio templates must contain the identical freestanding bitmap font.");
}
foreach (string required in new[]
{
    "NovaOryn Mono 8x16",
    "GlyphWidth = 8U",
    "GlyphHeight = 16U",
    "CharacterAdvance = 10U",
    "LineHeight = 20U",
    "DefaultFontSize = 32U",
    "GetRenderedGlyphWidth",
    "GetRenderedCharacterAdvance",
    "GetRenderedLineHeight",
    "GetSourceRow",
    "GetSourceColumn",
    "GetGlyphRow"
})
{
    if (!bootstrapBitmapFont.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Freestanding framebuffer font is missing its real-font contract: {required}");
    }
}
if (bootstrapBitmapFont.Contains("switch (value)", StringComparison.Ordinal) ||
    reusableBitmapFont.Contains("switch (value)", StringComparison.Ordinal))
{
    failures.Add("Framebuffer glyph dispatch must use the branch-only bit tree rather than a dense switch table.");
}
ulong[] expectedGlyphTop = new ulong[0x7F];
ulong[] expectedGlyphBottom = new ulong[0x7F];
for (int character = 0x20; character <= 0x7E; character++)
{
    string glyphPattern = $@"Top{character:X2} = 0x([0-9A-F]{{16}})UL, Bottom{character:X2} = 0x([0-9A-F]{{16}})UL;";
    Match freestandingGlyph = Regex.Match(bootstrapBitmapFont, glyphPattern, RegexOptions.CultureInvariant);
    Match reusableGlyph = Regex.Match(reusableBitmapFont, glyphPattern, RegexOptions.CultureInvariant);
    if (!freestandingGlyph.Success || !reusableGlyph.Success)
    {
        failures.Add($"NovaOryn Mono must define both 8-row halves for printable ASCII 0x{character:X2}.");
        continue;
    }
    if (!string.Equals(freestandingGlyph.Groups[1].Value, reusableGlyph.Groups[1].Value, StringComparison.Ordinal) ||
        !string.Equals(freestandingGlyph.Groups[2].Value, reusableGlyph.Groups[2].Value, StringComparison.Ordinal))
    {
        failures.Add($"Reusable and freestanding font data differ for printable ASCII 0x{character:X2}.");
    }
    expectedGlyphTop[character] = Convert.ToUInt64(freestandingGlyph.Groups[1].Value, 16);
    expectedGlyphBottom[character] = Convert.ToUInt64(freestandingGlyph.Groups[2].Value, 16);
}
Type? reusableBitmapFontType = typeof(NovaOryn.Console.Framebuffer.FramebufferConsole).Assembly.GetType("NovaOryn.Console.Framebuffer.BitmapFont");
System.Reflection.MethodInfo? reusableGetGlyphRow = reusableBitmapFontType?.GetMethod(
    "GetGlyphRow",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
if (reusableGetGlyphRow is null)
{
    failures.Add("Reusable framebuffer glyph-row function could not be loaded for dispatch testing.");
}
else
{
    for (int character = 0x20; character <= 0x7E; character++)
    {
        for (uint row = 0; row < 16U; row++)
        {
            ulong packed = row < 8U ? expectedGlyphTop[character] : expectedGlyphBottom[character];
            uint rowInHalf = row < 8U ? row : row - 8U;
            byte expectedRow = (byte)((packed >> (int)((7U - rowInHalf) * 8U)) & 0xFFUL);
            object? result = reusableGetGlyphRow.Invoke(null, new object?[] { (char)character, row });
            if (result is not byte actualRow || actualRow != expectedRow)
            {
                failures.Add($"Reusable framebuffer bit-tree dispatch failed for ASCII 0x{character:X2}, row {row}.");
                break;
            }
        }
    }
}
foreach (int descender in new[] { 0x67, 0x6A, 0x70, 0x71, 0x79 })
{
    Match descenderGlyph = Regex.Match(
        bootstrapBitmapFont,
        $@"Top{descender:X2} = 0x([0-9A-F]{{16}})UL, Bottom{descender:X2} = 0x([0-9A-F]{{16}})UL;",
        RegexOptions.CultureInvariant);
    if (!descenderGlyph.Success || (Convert.ToUInt64(descenderGlyph.Groups[2].Value, 16) & 0xFFFFFFUL) == 0)
    {
        failures.Add($"NovaOryn Mono descender U+{descender:X4} must draw below the baseline.");
    }
}
string thirdPartyNotices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.md"));
if (!thirdPartyNotices.Contains("DejaVu Sans Mono Bold 2.37", StringComparison.Ordinal) ||
    !thirdPartyNotices.Contains("Bitstream Vera Fonts Copyright", StringComparison.Ordinal))
{
    failures.Add("The embedded framebuffer font must retain its DejaVu/Bitstream provenance notice.");
}
string kernelConsole = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!kernelConsole.Contains("_framebuffer.Initialize(boot, fontSize)", StringComparison.Ordinal) ||
    !kernelConsole.Contains("_framebuffer.Clear()", StringComparison.Ordinal) ||
    !kernelConsole.Contains("Native.WriteSerial(value)", StringComparison.Ordinal) ||
    !kernelConsole.Contains("_framebuffer.Write(value)", StringComparison.Ordinal))
{
    failures.Add("The managed console assembly must initialize and clear the framebuffer and mirror each serial character to it.");
}

string framebufferProject = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Console.Framebuffer", "NovaOryn.Console.Framebuffer.csproj"));
string framebufferAssembly = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Console.Framebuffer", "FramebufferConsole.cs"));
if (!framebufferProject.Contains("<AssemblyName>NovaOryn.Console.Framebuffer</AssemblyName>", StringComparison.Ordinal) ||
    !framebufferAssembly.Contains("public sealed unsafe class FramebufferConsole : IConsole", StringComparison.Ordinal))
{
    failures.Add("The reusable managed framebuffer console assembly is not present.");
}
if (!framebufferAssembly.Contains("public uint FontSize { get; }", StringComparison.Ordinal) ||
    !framebufferAssembly.Contains("public uint FontSize => _configuration.FontSize;", StringComparison.Ordinal) ||
    !framebufferAssembly.Contains("Default(uint fontSize)", StringComparison.Ordinal) ||
    !framebufferAssembly.Contains("configuration.FontSize", StringComparison.Ordinal) ||
    !framebufferAssembly.Contains("BitmapFont.GetRenderedGlyphWidth(fontSize)", StringComparison.Ordinal) ||
    framebufferAssembly.Contains("public uint Scale { get; }", StringComparison.Ordinal) ||
    framebufferAssembly.Contains("configuration.Scale", StringComparison.Ordinal))
{
    failures.Add("The reusable framebuffer renderer must use FontSize as its single rendered-size input and must not expose a separate Scale setting.");
}
if (!solution.Contains("NovaOryn.Console.Framebuffer", StringComparison.Ordinal) ||
    !kernel.Contains("FramebufferConsole", StringComparison.Ordinal) ||
    !kernel.Contains("FramebufferConfiguration.Default(32U)", StringComparison.Ordinal) ||
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

string projectCreatorDefaults = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ProjectCreator", "Program.cs"));
if (!projectCreatorDefaults.Contains("Source", StringComparison.Ordinal) || !projectCreatorDefaults.Contains("Repos", StringComparison.Ordinal) || !projectCreatorDefaults.Contains("NovaOrynKernel", StringComparison.Ordinal))
{
    failures.Add("Project creator must default to the user Source\\Repos\\NovaOrynKernel directory.");
}
if (!buildScript.Contains("src\\NovaOryn.Kernel.Bootstrap", StringComparison.Ordinal) || !buildScript.Contains("NovaOrynProject.json", StringComparison.Ordinal))
{
    failures.Add("Build script must compile the authoritative in-repository bootstrap by default.");
}
if (!buildScript.Contains("Refreshing the existing external NovaOryn kernel project", StringComparison.Ordinal) ||
    !buildScript.Contains("$dotnet $projectCreator create --output $externalKernelDirectory --sdk-root $root", StringComparison.Ordinal))
{
    failures.Add("Build script must safely refresh an existing external NovaOryn kernel project.");
}
if (!buildScript.Contains("Refreshing the selected NovaOryn project before compilation", StringComparison.Ordinal) ||
    !buildScript.Contains("$dotnet $projectCreator create --output $selectedProjectDirectory --sdk-root $root", StringComparison.Ordinal) ||
    !buildScript.Contains("Selected user kernel is high-level only", StringComparison.Ordinal))
{
    failures.Add("Build script must refresh and validate the project selected by Visual Studio before compiling it.");
}
if (!projectCreatorDefaults.Contains("IsSdkGeneratedLowLevelKernel", StringComparison.Ordinal) ||
    !projectCreatorDefaults.Contains("\"DllImport\"", StringComparison.Ordinal) ||
    !projectCreatorDefaults.Contains("\"WritePort8\"", StringComparison.Ordinal) ||
    !projectCreatorDefaults.Contains("\"FramebufferConsole\"", StringComparison.Ordinal) ||
    !projectCreatorDefaults.Contains("\"NovaOrynManagedEntry\"", StringComparison.Ordinal) ||
    projectCreatorDefaults.Contains(".pre-0.0.69.bak", StringComparison.Ordinal) ||
    projectCreatorDefaults.Contains(".pre-0.0.74.bak", StringComparison.Ordinal) ||
    !projectCreatorDefaults.Contains("File.Delete(legacyRootKernel)", StringComparison.Ordinal))
{
    failures.Add("Project creator must remove SDK-generated monolithic kernels without preserving backups.");
}
string kernelTemplate = Path.Combine(root, "templates", "NovaOrynKernel", "Kernel", "Kernel.cs");
if (!File.Exists(kernelTemplate))
{
    failures.Add("External C# kernel project template is missing Kernel.cs.");
}
string commandLineTemplateRoot = Path.Combine(root, "templates", "NovaOrynKernel");
foreach (string wrapperName in new[] { "Build-Kernel.bat", "Run-Kernel.bat", "README-Kernel.md" })
{
    string commandLineWrapper = Path.Combine(commandLineTemplateRoot, wrapperName);
    if (!File.Exists(commandLineWrapper))
    {
        failures.Add($"Command-line kernel template is missing project helper: {wrapperName}");
    }
}
string buildKernelWrapper = File.ReadAllText(Path.Combine(commandLineTemplateRoot, "Build-Kernel.bat"));
string runKernelWrapper = File.ReadAllText(Path.Combine(commandLineTemplateRoot, "Run-Kernel.bat"));
if (!buildKernelWrapper.Contains("Build-NovaOryn.bat", StringComparison.Ordinal) ||
    !buildKernelWrapper.Contains("-Project", StringComparison.Ordinal) ||
    !buildKernelWrapper.Contains("-NoRun", StringComparison.Ordinal) ||
    !runKernelWrapper.Contains("Build-NovaOryn.bat", StringComparison.Ordinal) ||
    !runKernelWrapper.Contains("-Project", StringComparison.Ordinal) ||
    !runKernelWrapper.Contains("-Run", StringComparison.Ordinal))
{
    failures.Add("Kernel project wrappers must invoke the authoritative SDK pipeline with the selected project manifest.");
}

string visualStudioTemplateRoot = Path.Combine(root, "src", "NovaOryn.VisualStudio", "ProjectTemplates", "CSharp", "1033", "NovaOrynKernel");
string visualStudioKernelPath = Path.Combine(visualStudioTemplateRoot, "Kernel", "Kernel.cs");
foreach (string wrapperName in new[] { "Build-Kernel.bat", "Run-Kernel.bat", "README-Kernel.md" })
{
    string commandLineWrapper = File.ReadAllText(Path.Combine(commandLineTemplateRoot, wrapperName));
    string visualStudioWrapperPath = Path.Combine(visualStudioTemplateRoot, wrapperName);
    if (!File.Exists(visualStudioWrapperPath) ||
        !string.Equals(commandLineWrapper, File.ReadAllText(visualStudioWrapperPath), StringComparison.Ordinal))
    {
        failures.Add($"Visual Studio and command-line templates must ship the same project helper: {wrapperName}");
    }
}
if (!File.Exists(visualStudioKernelPath))
{
    failures.Add("Visual Studio kernel template is missing Kernel/Kernel.cs.");
}
else
{
    string visualStudioKernel = File.ReadAllText(visualStudioKernelPath);
    string commandLineKernel = File.ReadAllText(kernelTemplate);
    if (!string.Equals(visualStudioKernel, commandLineKernel, StringComparison.Ordinal))
    {
        failures.Add("Visual Studio and command-line templates must ship the same high-level user Kernel.cs.");
    }
    foreach (string forbiddenToken in new[] { "DllImport", "class Native", "WritePort8", "RuntimeExport", "NativeEntry", "FramebufferConsole", "0x3F8" })
    {
        if (visualStudioKernel.Contains(forbiddenToken, StringComparison.Ordinal))
        {
            failures.Add($"Visual Studio user Kernel.cs exposes low-level token: {forbiddenToken}");
        }
    }
    foreach (string requiredToken in new[] { "KernelConsole.WriteLine", "KernelPlatform.InitializeDescriptors", "KernelPlatform.InitializeInterrupts", "KernelPlatform.DisableLegacyPic", "KernelPlatform.Halt" })
    {
        if (!visualStudioKernel.Contains(requiredToken, StringComparison.Ordinal))
        {
            failures.Add($"Visual Studio user Kernel.cs is missing high-level call: {requiredToken}");
        }
    }
}
foreach (string obsoleteTemplateFile in new[]
{
    Path.Combine("Boot", "BootContext.cs"),
    Path.Combine("Console", "FramebufferConsole.cs"),
    Path.Combine("Console", "BitmapFont.cs"),
    Path.Combine("Runtime", "CoreLib.cs")
})
{
    if (File.Exists(Path.Combine(visualStudioTemplateRoot, obsoleteTemplateFile)))
    {
        failures.Add($"Visual Studio template still exposes obsolete monolithic source: {obsoleteTemplateFile}");
    }
}
string visualStudioKernelProject = File.ReadAllText(Path.Combine(visualStudioTemplateRoot, "NovaOrynKernel.csproj"));
foreach (string requiredProjectReference in new[]
{
    "NovaOryn.Freestanding.CoreLib.csproj",
    "NovaOryn.Kernel.Console.csproj",
    "NovaOryn.Kernel.Platform.X64.csproj"
})
{
    if (!visualStudioKernelProject.Contains(requiredProjectReference, StringComparison.Ordinal))
    {
        failures.Add($"Visual Studio kernel project is missing separated assembly reference: {requiredProjectReference}");
    }
}
if (visualStudioKernelProject.Contains("Runtime\\CoreLib.cs", StringComparison.Ordinal) ||
    visualStudioKernelProject.Contains("Boot\\BootContext.cs", StringComparison.Ordinal) ||
    visualStudioKernelProject.Contains("Console\\FramebufferConsole.cs", StringComparison.Ordinal))
{
    failures.Add("Visual Studio kernel project must not compile low-level support into the user assembly.");
}
string visualStudioManifest = File.ReadAllText(Path.Combine(visualStudioTemplateRoot, "NovaOrynProject.json"));
if (!visualStudioManifest.Contains("Sdk/NovaOryn.Kernel.Entry.X64/NovaOryn.Kernel.Entry.X64.csproj", StringComparison.Ordinal))
{
    failures.Add("Visual Studio project manifest must compile through the separate x64 entry assembly.");
}
string visualStudioEntryProject = File.ReadAllText(Path.Combine(visualStudioTemplateRoot, "Sdk", "NovaOryn.Kernel.Entry.X64", "NovaOryn.Kernel.Entry.X64.csproj"));
if (!visualStudioEntryProject.Contains("..\\..\\$safeprojectname$.csproj", StringComparison.Ordinal) ||
    visualStudioEntryProject.Contains("..\\..\\..\\$safeprojectname$.csproj", StringComparison.Ordinal))
{
    failures.Add("Visual Studio entry project must reference the generated user project exactly two directories above the entry assembly.");
}
string commandLineEntryProject = File.ReadAllText(Path.Combine(root, "templates", "NovaOrynKernel", "Sdk", "NovaOryn.Kernel.Entry.X64", "NovaOryn.Kernel.Entry.X64.csproj"));
if (!commandLineEntryProject.Contains("..\\..\\NovaOrynKernel.csproj", StringComparison.Ordinal) ||
    commandLineEntryProject.Contains("..\\..\\..\\NovaOrynKernel.csproj", StringComparison.Ordinal))
{
    failures.Add("Command-line entry project must reference the user kernel exactly two directories above the entry assembly.");
}
string visualStudioLowLevel = File.ReadAllText(Path.Combine(visualStudioTemplateRoot, "Sdk", "NovaOryn.Kernel.X64.LowLevel", "Native.cs"));
if (!visualStudioLowLevel.Contains("class Native", StringComparison.Ordinal) || !visualStudioLowLevel.Contains("DllImport", StringComparison.Ordinal))
{
    failures.Add("Visual Studio template must keep native imports in the separate low-level assembly.");
}
string visualStudioTemplateConsole = File.ReadAllText(Path.Combine(visualStudioTemplateRoot, "Sdk", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!visualStudioTemplateConsole.Contains("public static Boolean Write(String value)", StringComparison.Ordinal) ||
    !visualStudioTemplateConsole.Contains("public static Boolean WriteLine(String value)", StringComparison.Ordinal) ||
    !visualStudioTemplateConsole.Contains("Initialize(BootContext boot, UInt32 fontSize)", StringComparison.Ordinal) ||
    visualStudioTemplateConsole.Contains("unsafe Boolean Write", StringComparison.Ordinal) ||
    visualStudioTemplateConsole.Contains("WritePort8", StringComparison.Ordinal) ||
    visualStudioTemplateConsole.Contains("0x3F8", StringComparison.Ordinal))
{
    failures.Add("Visual Studio template must expose normal managed Write and WriteLine functions while hiding raw serial I/O in the low-level assembly.");
}
string visualStudioVstemplate = File.ReadAllText(Path.Combine(visualStudioTemplateRoot, "NovaOrynKernel.vstemplate"));
if (!visualStudioVstemplate.Contains("NovaOryn.Kernel.X64.LowLevel", StringComparison.Ordinal) ||
    visualStudioVstemplate.Contains("<Folder Name=\"Runtime\"", StringComparison.Ordinal) ||
    visualStudioVstemplate.Contains("<Folder Name=\"Boot\"", StringComparison.Ordinal) ||
    visualStudioVstemplate.Contains("<Folder Name=\"Console\"", StringComparison.Ordinal))
{
    failures.Add("Visual Studio .vstemplate must package separated SDK assemblies and reject the obsolete monolithic folders.");
}
string vsixBuildScript = File.ReadAllText(Path.Combine(root, "Build-NovaOrynVSIX.ps1"));
if (!vsixBuildScript.Contains("NovaOryn.Kernel.X64.LowLevel/Native.cs", StringComparison.Ordinal) ||
    !vsixBuildScript.Contains("VSIX user Kernel.cs exposes low-level token", StringComparison.Ordinal) ||
    !vsixBuildScript.Contains("obsolete monolithic template content", StringComparison.Ordinal))
{
    failures.Add("VSIX build validation must inspect the exact high-level kernel and separated template payload.");
}

string descriptorContracts = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Architecture.Contracts", "DescriptorContracts.cs"));
foreach (string required in new[] { "SegmentSelector", "DescriptorPrivilegeLevel", "GlobalDescriptorTableConfiguration", "TaskStateSegmentConfiguration", "IGlobalDescriptorTable", "ITaskStateSegment", "IoPermissionBitmapPolicy" })
{
    if (!descriptorContracts.Contains(required, StringComparison.Ordinal)) failures.Add($"Descriptor contracts are missing {required}.");
}
string descriptorImplementation = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Architecture.X64.Descriptors", "X64GlobalDescriptorTable.cs"));
string taskStateImplementation = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Architecture.X64.Descriptors", "X64TaskStateSegment.cs"));
foreach (string required in new[] { "0x00AF9A000000FFFFUL", "0x00CF92000000FFFFUL", "0x00AFFA000000FFFFUL", "WriteTaskStateDescriptor", "TaskStateSelector" })
{
    if (!descriptorImplementation.Contains(required, StringComparison.Ordinal)) failures.Add($"x64 GDT implementation is missing {required}.");
}
foreach (string required in new[] { "RingZeroStackTop", "DoubleFaultStackTop", "NmiStackTop", "MachineCheckStackTop", "IoPermissionBitmapPolicy", "SetInterruptStack" })
{
    if (!taskStateImplementation.Contains(required, StringComparison.Ordinal)) failures.Add($"x64 TSS implementation is missing {required}.");
}
string descriptorAssembly = File.ReadAllText(Path.Combine(root, "native", "x64", "Descriptors.asm"));
foreach (string required in new[] { "lgdt", "ltr ax", "retfq", "mov ds, ax", "mov ss, ax" })
{
    if (!descriptorAssembly.Contains(required, StringComparison.Ordinal)) failures.Add($"Native descriptor support is missing {required}.");
}
if (!buildScript.Contains("Descriptors.asm", StringComparison.Ordinal) || !linker.Contains("Descriptors.obj", StringComparison.Ordinal))
{
    failures.Add("Descriptor native object must be assembled and linked into the EFI image.");
}
if (!solution.Contains("NovaOryn.Architecture.X64.Descriptors", StringComparison.Ordinal))
{
    failures.Add("The authoritative solution must include the x64 descriptors assembly.");
}


string interruptContracts = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Interrupts.Contracts", "InterruptContracts.cs"));
foreach (string required in new[] { "InterruptContext", "InterruptHandler", "InterruptResult", "InterruptRegistrationResult", "IInterruptDescriptorTable", "IInterruptVectorAllocator", "IExceptionDiagnosticSink", "ControlRegister2", "PrivilegeTransition" })
{
    if (!interruptContracts.Contains(required, StringComparison.Ordinal)) failures.Add($"Interrupt contracts are missing {required}.");
}
string idtImplementation = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Architecture.X64.Interrupts", "X64InterruptDescriptorTable.cs"));
foreach (string required in new[] { "EntryCount = 256", "WriteGate", "GetInterruptStub", "InterruptGateType", "InterruptStackTable", "DispatchNative", "Remove(" })
{
    if (!idtImplementation.Contains(required, StringComparison.Ordinal)) failures.Add($"x64 IDT implementation is missing {required}.");
}
string exceptionHandlers = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Architecture.X64.Interrupts", "EssentialExceptionHandlers.cs"));
foreach (string required in new[] { "Divide error", "Invalid opcode", "General protection fault", "Page fault", "Double fault", "Stack-segment fault", "Non-maskable interrupt", "Machine check", "Current thread/process", "Stack trace" })
{
    if (!exceptionHandlers.Contains(required, StringComparison.Ordinal)) failures.Add($"Essential exception diagnostics are missing {required}.");
}
string interruptAssembly = File.ReadAllText(Path.Combine(root, "native", "x64", "Interrupts.asm"));
foreach (string required in new[] { "NovaOrynX64InterruptStub0", "NovaOrynX64InterruptStub255", "NovaOrynX64InterruptStubTable", "lidt", "iretq", "mov rax, cr2", "push qword 0", "NovaOrynX64StopProcessor", "NovaOrynX64SetInterruptStackSwitch" })
{
    if (!interruptAssembly.Contains(required, StringComparison.Ordinal)) failures.Add($"Native interrupt support is missing {required}.");
}
if (!buildScript.Contains("Interrupts.asm", StringComparison.Ordinal) || !linker.Contains("Interrupts.obj", StringComparison.Ordinal))
{
    failures.Add("Interrupt native object must be assembled and linked into the EFI image.");
}
if (!solution.Contains("NovaOryn.Interrupts.Contracts", StringComparison.Ordinal) || !solution.Contains("NovaOryn.Architecture.X64.Interrupts", StringComparison.Ordinal))
{
    failures.Add("The authoritative solution must include the interrupt contracts and x64 implementation assemblies.");
}


string controllerContracts = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.InterruptControllers.Contracts", "InterruptControllerContracts.cs"));
foreach (string required in new[] { "IInterruptController", "ILegacyPic", "InterruptRouteConfiguration", "InterruptDeliveryMechanism", "InterruptPolarity", "InterruptTriggerMode", "InterruptAffinity", "EndOfInterrupt", "SendInterprocessorInterrupt", "CreateMessage" })
{
    if (!controllerContracts.Contains(required, StringComparison.Ordinal)) failures.Add($"Interrupt-controller contracts are missing {required}.");
}
string controllerImplementation = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.InterruptControllers.X64", "X64InterruptController.cs"));
foreach (string required in new[] { "LegacyPic", "ProgramIoApic", "CreateMessage", "X2ApicEoiMsr", "X2ApicIcrMsr", "SetAffinity", "SetPriority", "IInterruptVectorAllocator" })
{
    if (!controllerImplementation.Contains(required, StringComparison.Ordinal)) failures.Add($"x64 interrupt-controller implementation is missing {required}.");
}
string controllerAssembly = File.ReadAllText(Path.Combine(root, "native", "x64", "InterruptControllers.asm"));
foreach (string required in new[] { "in al, dx", "out dx, al", "rdmsr", "wrmsr", "mfence", "NovaOrynX64WriteMmio32" })
{
    if (!controllerAssembly.Contains(required, StringComparison.Ordinal)) failures.Add($"Native interrupt-controller support is missing {required}.");
}
string cpuAssembly = File.ReadAllText(Path.Combine(root, "native", "x64", "Cpu.asm"));
foreach (string symbol in new[] { "NovaOrynX64ReadPort8", "NovaOrynX64WritePort8" })
{
    if (controllerAssembly.Contains($"global {symbol}", StringComparison.Ordinal)) failures.Add($"InterruptControllers.asm must not duplicate the CPU native symbol {symbol}.");
}
foreach (string symbol in new[] { "NovaOrynX64ControllerReadPort8", "NovaOrynX64ControllerWritePort8" })
{
    if (!controllerAssembly.Contains($"global {symbol}", StringComparison.Ordinal)) failures.Add($"InterruptControllers.asm is missing its namespaced native symbol {symbol}.");
    if (cpuAssembly.Contains($"global {symbol}", StringComparison.Ordinal)) failures.Add($"Cpu.asm must not duplicate the interrupt-controller native symbol {symbol}.");
}
if (!buildScript.Contains("InterruptControllers.asm", StringComparison.Ordinal) || !linker.Contains("InterruptControllers.obj", StringComparison.Ordinal))
{
    failures.Add("Interrupt-controller native object must be assembled and linked into the EFI image.");
}
if (!solution.Contains("NovaOryn.InterruptControllers.Contracts", StringComparison.Ordinal) || !solution.Contains("NovaOryn.InterruptControllers.X64", StringComparison.Ordinal))
{
    failures.Add("The authoritative solution must include interrupt-controller contracts and the x64 implementation.");
}

string updaterSource = File.ReadAllText(Path.Combine(root, "Update-NovaOryn.ps1"));
if (!updaterSource.Contains("Get-ArchiveDeclaredDeletionSet", StringComparison.Ordinal) ||
    !updaterSource.Contains("Get-ArchiveTargetPathSet", StringComparison.Ordinal) ||
    !updaterSource.Contains("$isMissingLocally -and -not $targetPaths.Contains($normalized)", StringComparison.Ordinal))
{
    failures.Add("Updater must accept locally absent carried-forward deletions absent from the selected target source manifest.");
}
if (!updaterSource.Contains("$statusCode[0] -eq 'D'", StringComparison.Ordinal) ||
    !updaterSource.Contains("$statusCode[1] -eq 'D'", StringComparison.Ordinal))
{
    failures.Add("Updater must recognise tracked deletions in either Git porcelain status column.");
}
if (!updaterSource.Contains("NovaOryn-SourceManifest.json", StringComparison.Ordinal) ||
    !updaterSource.Contains("archiveHashes.ContainsKey($normalized)", StringComparison.Ordinal))
{
    failures.Add("Updater must permit the selected archive to replace generated source-manifest metadata.");
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
Console.WriteLine("[ OK ] NovaOryn Mono 8x16 covers printable ASCII with retained descenders.");
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

// Release policy: Update-NovaOryn.bat must launch Bootstrap-Update-NovaOryn.ps1 so updater fixes run from the selected archive.
