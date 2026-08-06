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
    !kernelEntry.Contains("Kernel.KMain(new BootContext(bootContextAddress))", StringComparison.Ordinal))
{
    failures.Add("The separate x64 entry assembly must own the runtime export and dispatch to KMain.");
}
string bootstrapManifest = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "NovaOrynProject.json"));
if (!bootstrapManifest.Contains("NovaOryn.Kernel.Entry.X64.csproj", StringComparison.Ordinal))
{
    failures.Add("The authoritative bootstrap manifest must compile through the separate entry assembly.");
}
string projectCreator = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ProjectCreator", "Program.cs"));
if (!projectCreator.Contains("IsSdkGeneratedLegacyKernel", StringComparison.Ordinal) ||
    !projectCreator.Contains(".pre-0.0.69.bak", StringComparison.Ordinal))
{
    failures.Add("Project creation must migrate SDK-generated low-level Kernel.cs files while retaining a backup.");
}
string lowLevelAssembly = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.X64.LowLevel", "Native.cs"));
foreach (string nativeMember in new[] { "class Native", "WritePort8", "InitializeBootstrapDescriptors", "InitializeBootstrapInterrupts", "DisableLegacyPic", "Halt" })
{
    if (!lowLevelAssembly.Contains(nativeMember, StringComparison.Ordinal)) failures.Add($"The low-level x64 assembly is missing {nativeMember}.");
}
string managedKernelConsole = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!managedKernelConsole.Contains("public static Boolean Write(String value)", StringComparison.Ordinal) ||
    !managedKernelConsole.Contains("public static Boolean WriteLine(String value)", StringComparison.Ordinal))
{
    failures.Add("Freestanding Write and WriteLine must be normal managed C# functions.");
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

string bootstrapBootContext = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "BootContext.cs"));
foreach (string required in new[] { "FramebufferAddress", "FramebufferSize", "PixelsPerScanLine", "PixelFormat", "RedMask", "GreenMask", "BlueMask" })
{
    if (!bootstrapBootContext.Contains(required, StringComparison.Ordinal))
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
string kernelConsole = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Console", "KernelConsole.cs"));
if (!kernelConsole.Contains("_framebuffer.Initialize(boot)", StringComparison.Ordinal) ||
    !kernelConsole.Contains("_framebuffer.Clear()", StringComparison.Ordinal) ||
    !kernelConsole.Contains("Native.WritePort8(0x3F8, value)", StringComparison.Ordinal) ||
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
if (!buildScript.Contains("src\\NovaOryn.Kernel.Bootstrap", StringComparison.Ordinal) || !buildScript.Contains("NovaOrynProject.json", StringComparison.Ordinal))
{
    failures.Add("Build script must compile the authoritative in-repository bootstrap by default.");
}
string kernelTemplate = Path.Combine(root, "templates", "NovaOrynKernel", "Kernel", "Kernel.cs");
if (!File.Exists(kernelTemplate))
{
    failures.Add("External C# kernel project template is missing Kernel.cs.");
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
