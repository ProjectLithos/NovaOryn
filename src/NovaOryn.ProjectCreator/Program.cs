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
    if (!MigrateLegacyRootKernel(output)) return 1;

    foreach (string source in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(template, source);
        string destination = Path.Combine(output, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (string.Equals(relative, "NovaOrynProject.json", StringComparison.OrdinalIgnoreCase)) continue;
        bool userKernelSource = string.Equals(relative, Path.Combine("Kernel", "Kernel.cs"), StringComparison.OrdinalIgnoreCase);
        if (userKernelSource && File.Exists(destination) && !IsSdkGeneratedLegacyKernel(destination)) continue;
        if (userKernelSource && File.Exists(destination)) File.Copy(destination, destination + ".pre-0.0.69.bak", true);
        File.Copy(source, destination, true);
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
    Console.WriteLine($"[ OK ] Kernel solution : {Path.Combine(output, "NovaOrynKernel.sln")}");
    Console.WriteLine($"[ OK ] Project manifest: {manifestPath}");
    return 0;
}



static bool MigrateLegacyRootKernel(string output)
{
    string legacyRootKernel = Path.Combine(output, "Kernel.cs");
    if (!File.Exists(legacyRootKernel)) return true;

    if (!IsSdkGeneratedLegacyKernel(legacyRootKernel))
    {
        Console.Error.WriteLine($"[FAIL] A user-owned legacy root Kernel.cs prevents migration: {legacyRootKernel}");
        Console.Error.WriteLine("[FAIL] Move that file to Kernel\\Kernel.cs or remove it before refreshing the SDK project.");
        return false;
    }

    string backup = legacyRootKernel + ".pre-0.0.73.bak";
    File.Copy(legacyRootKernel, backup, true);
    File.Delete(legacyRootKernel);
    Console.WriteLine($"[ OK ] Migrated legacy root kernel: {legacyRootKernel}");
    Console.WriteLine($"[ OK ] Legacy kernel backup : {backup}");
    return true;
}

static bool IsSdkGeneratedLegacyKernel(string path)
{
    string source = File.ReadAllText(path);
    bool monolithic = source.Contains("internal static class Native", StringComparison.Ordinal) &&
        source.Contains("WriteLineDescriptors", StringComparison.Ordinal) &&
        source.Contains("InitializeSerial", StringComparison.Ordinal);
    bool previousGenerated = source.Contains("[RuntimeExport(\"NovaOrynManagedEntry\")]", StringComparison.Ordinal) &&
        source.Contains("KernelPlatform.InitializeDescriptors", StringComparison.Ordinal) &&
        source.Contains("KernelConsole.WriteLine", StringComparison.Ordinal);
    return monolithic || previousGenerated;
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
