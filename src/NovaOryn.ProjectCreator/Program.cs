using System.Text.Json;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 1 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
    {
        return Fail("Usage: NovaOryn.ProjectCreator create [--output <directory>] [--sdk-root <directory>]");
    }

    string sdkRoot = Path.GetFullPath(GetOption(args, "--sdk-root") ?? FindSdkRoot(AppContext.BaseDirectory));
    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string output = Path.GetFullPath(GetOption(args, "--output") ?? Path.Combine(userProfile, "Source", "Repos", "NovaOrynKernel"));
    string template = Path.Combine(sdkRoot, "templates", "NovaOrynKernel");
    if (!Directory.Exists(template)) return Fail($"Kernel project template was not found: {template}");

    Directory.CreateDirectory(output);
    string? mainProjectPath = ResolveMainProjectPath(output);
    if (mainProjectPath is null) return 1;
    if (!MigrateLegacyRootKernel(output)) return 1;
    if (!RemoveSdkOwnedLegacyTrees(output)) return 1;

    foreach (string source in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(template, source);
        if (string.Equals(relative, "NovaOrynProject.json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relative, "NovaOrynKernel.csproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relative, "NovaOrynKernel.sln", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string destination = Path.Combine(output, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        bool userKernelSource = string.Equals(relative, Path.Combine("Kernel", "Kernel.cs"), StringComparison.OrdinalIgnoreCase);
        if (userKernelSource && File.Exists(destination) && !IsSdkGeneratedLowLevelKernel(destination)) continue;
        File.Copy(source, destination, true);
    }

    string mainProjectFileName = Path.GetFileName(mainProjectPath);
    if (string.IsNullOrWhiteSpace(mainProjectFileName)) return Fail($"Kernel project filename is invalid: {mainProjectPath}");
    File.Copy(Path.Combine(template, "NovaOrynKernel.csproj"), mainProjectPath, true);
    string entryProjectPath = Path.Combine(output, "Sdk", "NovaOryn.Kernel.Entry.X64", "NovaOryn.Kernel.Entry.X64.csproj");
    string entryProject = File.ReadAllText(entryProjectPath);
    entryProject = entryProject.Replace(
        Path.Combine("..", "..", "NovaOrynKernel.csproj"),
        Path.Combine("..", "..", mainProjectFileName),
        StringComparison.OrdinalIgnoreCase);
    File.WriteAllText(entryProjectPath, entryProject);

    string solutionPath = ResolveSolutionPath(output);
    if (!File.Exists(solutionPath))
    {
        string solution = File.ReadAllText(Path.Combine(template, "NovaOrynKernel.sln"));
        solution = solution.Replace("NovaOrynKernel.csproj", mainProjectFileName, StringComparison.Ordinal);
        solution = solution.Replace("\"NovaOrynKernel\"", $"\"{Path.GetFileNameWithoutExtension(mainProjectPath)}\"", StringComparison.Ordinal);
        File.WriteAllText(solutionPath, solution);
    }

    string manifestPath = Path.Combine(output, "NovaOrynProject.json");
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
    {
        Name = "MinimalKernel",
        ProjectFile = "Sdk/NovaOryn.Kernel.Entry.X64/NovaOryn.Kernel.Entry.X64.csproj",
        TargetArchitecture = "x64",
        BootProtocol = "Uefi",
        KernelEntry = "KMain",
        RuntimePack = "NovaOryn.RuntimePack.X64.Bootstrap",
        OutputDirectory = Path.Combine(sdkRoot, "Artifacts", "MinimalKernel")
    }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

    Console.WriteLine($"[ OK ] C# kernel project: {output}");
    Console.WriteLine($"[ OK ] User kernel     : {Path.Combine(output, "Kernel", "Kernel.cs")}");
    Console.WriteLine($"[ OK ] Kernel project  : {mainProjectPath}");
    Console.WriteLine($"[ OK ] Kernel solution : {solutionPath}");
    Console.WriteLine($"[ OK ] Project manifest: {manifestPath}");
    return 0;
}

static string? ResolveMainProjectPath(string output)
{
    string[] candidates = Directory.EnumerateFiles(output, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();
    if (candidates.Length == 0) return Path.Combine(output, "NovaOrynKernel.csproj");
    if (candidates.Length == 1) return candidates[0];
    Console.Error.WriteLine($"[FAIL] More than one root kernel project exists in {output}: {string.Join(", ", candidates.Select(candidate => Path.GetFileName(candidate)))}");
    return null;
}

static string ResolveSolutionPath(string output)
{
    string? existing = Directory.EnumerateFiles(output, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
    return existing ?? Path.Combine(output, "NovaOrynKernel.sln");
}

static bool MigrateLegacyRootKernel(string output)
{
    string legacyRootKernel = Path.Combine(output, "Kernel.cs");
    if (!File.Exists(legacyRootKernel)) return true;

    if (!IsSdkGeneratedLowLevelKernel(legacyRootKernel))
    {
        Console.Error.WriteLine($"[FAIL] A user-owned legacy root Kernel.cs prevents migration: {legacyRootKernel}");
        Console.Error.WriteLine("[FAIL] Move that file to Kernel\\Kernel.cs or remove it before refreshing the SDK project.");
        return false;
    }

    File.Delete(legacyRootKernel);
    Console.WriteLine($"[ OK ] Removed generated legacy root kernel: {legacyRootKernel}");
    return true;
}

static bool RemoveSdkOwnedLegacyTrees(string output)
{
    foreach (string relative in new[] { "Boot", "Console", "Runtime", "Sdk" })
    {
        string path = Path.Combine(output, relative);
        if (!Directory.Exists(path)) continue;
        Directory.Delete(path, true);
        Console.WriteLine($"[ OK ] Refreshed SDK-owned project tree: {path}");
    }
    return true;
}

static bool IsSdkGeneratedLowLevelKernel(string path)
{
    string source = File.ReadAllText(path);
    bool exposesNativeInterop = source.Contains("DllImport", StringComparison.Ordinal) &&
        source.Contains("WritePort8", StringComparison.Ordinal) &&
        source.Contains("NovaOrynX64", StringComparison.Ordinal);
    bool monolithicConsole = source.Contains("FramebufferConsole", StringComparison.Ordinal) &&
        source.Contains("InitializeSerial", StringComparison.Ordinal) &&
        source.Contains("WriteLineDescriptors", StringComparison.Ordinal);
    bool exportedBootstrap = source.Contains("RuntimeExport", StringComparison.Ordinal) &&
        source.Contains("NovaOrynManagedEntry", StringComparison.Ordinal) &&
        source.Contains("KMain", StringComparison.Ordinal);
    return exposesNativeInterop && monolithicConsole && exportedBootstrap;
}

static string FindSdkRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "NovaOryn.sln"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("NovaOryn SDK root was not found.");
}

static string? GetOption(string[] args, string name)
{
    for (int index = 0; index + 1 < args.Length; index++)
    {
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
    }
    return null;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"[FAIL] {message}");
    return 1;
}
