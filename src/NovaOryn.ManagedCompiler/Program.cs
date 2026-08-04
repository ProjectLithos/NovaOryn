using System.Diagnostics;
using System.Text.Json;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "compile", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.ManagedCompiler compile <NovaOrynProject.json> [--dotnet <path>] [--configuration Debug|Release] [--dry-run]");

    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string dotnet = GetOption(args, "--dotnet") ?? "dotnet";
    string configuration = GetOption(args, "--configuration") ?? "Release";
    bool dryRun = HasOption(args, "--dry-run");
    string nativeOutput = Path.Combine(project.OutputDirectory, "NativeAot");
    Directory.CreateDirectory(nativeOutput);

    string[] publishArguments =
    [
        "publish", project.ProjectFile,
        "--configuration", configuration,
        "--runtime", "win-x64",
        "--self-contained", "true",
        "--output", nativeOutput,
        "--nologo",
        "-p:PublishAot=true",
        "-p:NativeLib=Static",
        "-p:NoStdLib=true",
        "-p:NoConfig=true",
        "-p:IlcSystemModule=NovaOryn.Kernel.Bootstrap",
        "-p:IlcGenerateCompleteTypeMetadata=false",
        "-p:IlcDisableReflection=true",
        "-p:IlcGenerateStackTraceData=false",
        "-p:StackTraceSupport=false",
        "-p:EventSourceSupport=false",
        "-p:DebuggerSupport=false",
        "-p:MetadataUpdaterSupport=false",
        "-p:BuiltInComInteropSupport=false",
        "-p:InvariantGlobalization=true",
        "-p:StripSymbols=false"
    ];

    Console.WriteLine($"[INFO] Compiling {project.Name} with NovaOryn.RuntimePack.X64.Bootstrap.");
    Console.WriteLine("[INFO] The stock Windows CoreLib and NativeAOT runtime libraries are intentionally excluded.");
    Console.WriteLine("[INFO] RID win-x64 selects Microsoft x64 ILC compiler assets only; it is not the NovaOryn runtime target.");
    if (dryRun) return 0;
    int exitCode = Run(dotnet, publishArguments, Path.GetDirectoryName(project.ProjectFile)!);
    if (exitCode != 0) return Fail($"NovaOryn bootstrap ILC publish failed with exit code {exitCode}.");

    string? nativeLibrary = Directory.GetFiles(nativeOutput, "*.lib", SearchOption.TopDirectoryOnly)
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault();
    if (nativeLibrary is null) return Fail($"ILC did not produce a static library in {nativeOutput}.");

    string compileManifest = Path.Combine(project.OutputDirectory, "NovaOryn.Compile.json");
    File.WriteAllText(compileManifest, JsonSerializer.Serialize(new
    {
        schemaVersion = 3,
        productVersion = "0.0.22",
        project = project.Name,
        kernelEntry = project.KernelEntry,
        architecture = project.TargetArchitecture,
        runtimePack = "NovaOryn.RuntimePack.X64.Bootstrap",
        runtimeMode = "NoGcBootstrap",
        nativeLibrary,
        runtimeLibraries = Array.Empty<string>(),
        producedUtc = DateTimeOffset.UtcNow
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"[ OK ] ILC produced freestanding library: {nativeLibrary}");
    Console.WriteLine("[ OK ] Windows platform runtime libraries linked: 0");
    Console.WriteLine($"[ OK ] Compilation manifest: {compileManifest}");
    return 0;
}

static int Run(string executable, IEnumerable<string> arguments, string workingDirectory)
{
    using Process process = new();
    process.StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = workingDirectory };
    foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    if (!process.Start()) return -1;
    process.WaitForExit();
    return process.ExitCode;
}
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
