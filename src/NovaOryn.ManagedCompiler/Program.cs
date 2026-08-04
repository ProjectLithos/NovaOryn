using System.Diagnostics;
using System.Text.Json;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "compile", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.ManagedCompiler compile <NovaOrynProject.json> [--dotnet <path>] [--configuration Debug|Release] [--dry-run]");

    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string dotnet = GetOption(args, "--dotnet") ?? Environment.GetEnvironmentVariable("NOVAORYN_DOTNET") ?? "dotnet";
    string configuration = GetOption(args, "--configuration") ?? "Release";
    bool dryRun = HasOption(args, "--dry-run");
    string output = project.OutputDirectory;
    string nativeOutput = Path.Combine(output, "NativeAot");
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
        "-p:IlcGenerateCompleteTypeMetadata=false",
        "-p:IlcDisableReflection=true",
        "-p:IlcGenerateStackTraceData=false",
        "-p:StackTraceSupport=false",
        "-p:EventSourceSupport=false",
        "-p:DebuggerSupport=false",
        "-p:MetadataUpdaterSupport=false",
        "-p:BuiltInComInteropSupport=false",
        "-p:UseSystemResourceKeys=true",
        "-p:StripSymbols=false",
        "-p:InvariantGlobalization=true",
        "-p:DebugType=embedded",
        "-p:DebugSymbols=true"
    ];

    Console.WriteLine($"[INFO] Compiling {project.Name} through the .NET NativeAOT/ILC pipeline.");
    Console.WriteLine($"[INFO] {dotnet} {JoinArguments(publishArguments)}");
    if (dryRun) return 0;
    DateTime publishStartedUtc = DateTime.UtcNow;
    int exitCode = Run(dotnet, publishArguments, Path.GetDirectoryName(project.ProjectFile)!);
    if (exitCode != 0) return Fail($"NativeAOT publish failed with exit code {exitCode}.");

    string[] libraries = Directory.GetFiles(nativeOutput, "*.lib", SearchOption.TopDirectoryOnly);
    if (libraries.Length == 0) return Fail($"ILC did not produce a native static library in {nativeOutput}.");
    string nativeLibrary = libraries.OrderByDescending(File.GetLastWriteTimeUtc).First();
    string[] runtimeLibraries = DiscoverRuntimeLibraries(project.ProjectFile, nativeLibrary, publishStartedUtc);
    if (runtimeLibraries.Length == 0)
        return Fail("The NativeAOT runtime libraries could not be located. The freestanding linker requires the installed NativeAOT runtime pack, not only the application static library.");

    string compileManifest = Path.Combine(output, "NovaOryn.Compile.json");
    File.WriteAllText(compileManifest, JsonSerializer.Serialize(new
    {
        schemaVersion = 2,
        productVersion = "0.0.19",
        project = project.Name,
        kernelEntry = project.KernelEntry,
        architecture = project.TargetArchitecture,
        nativeLibrary,
        runtimeLibraries,
        producedUtc = DateTimeOffset.UtcNow
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"[ OK ] ILC produced native library: {nativeLibrary}");
    Console.WriteLine($"[ OK ] NativeAOT runtime libraries discovered: {runtimeLibraries.Length}");
    Console.WriteLine($"[ OK ] Compilation manifest: {compileManifest}");
    return 0;
}

static string[] DiscoverRuntimeLibraries(string projectFile, string applicationLibrary, DateTime publishStartedUtc)
{
    HashSet<string> libraries = new(StringComparer.OrdinalIgnoreCase);
    string? packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
    if (string.IsNullOrWhiteSpace(packageRoot))
    {
        string? userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile)) packageRoot = Path.Combine(userProfile, ".nuget", "packages");
    }

    if (!string.IsNullOrWhiteSpace(packageRoot) && Directory.Exists(packageRoot))
    {
        string[] packageNames =
        [
            "microsoft.netcore.app.runtime.nativeaot.win-x64",
            "microsoft.dotnet.ilcompiler"
        ];

        foreach (string packageName in packageNames)
        {
            string packageDirectory = Path.Combine(packageRoot, packageName);
            if (!Directory.Exists(packageDirectory)) continue;
            foreach (string library in Directory.GetFiles(packageDirectory, "*.lib", SearchOption.AllDirectories))
                libraries.Add(Path.GetFullPath(library));
        }
    }

    string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
    string intermediateDirectory = Path.Combine(projectDirectory, "obj");
    if (Directory.Exists(intermediateDirectory))
    {
        foreach (string library in Directory.GetFiles(intermediateDirectory, "*.lib", SearchOption.AllDirectories))
        {
            if (File.GetLastWriteTimeUtc(library) < publishStartedUtc.AddMinutes(-2)) continue;
            libraries.Add(Path.GetFullPath(library));
        }
    }

    libraries.RemoveWhere(path =>
        string.Equals(Path.GetFullPath(path), Path.GetFullPath(applicationLibrary), StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".exports.lib", StringComparison.OrdinalIgnoreCase));

    return libraries
        .Where(File.Exists)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();
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
static string JoinArguments(IEnumerable<string> values) => string.Join(" ", values.Select(Quote));
static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
