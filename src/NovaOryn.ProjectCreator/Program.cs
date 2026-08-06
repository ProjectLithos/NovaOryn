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
    foreach (string source in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(template, source);
        string destination = Path.Combine(output, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (string.Equals(relative, "NovaOrynProject.json", StringComparison.OrdinalIgnoreCase)) continue;
        bool userKernelSource = string.Equals(relative, Path.Combine("Kernel", "Kernel.cs"), StringComparison.OrdinalIgnoreCase);
        if (userKernelSource && File.Exists(destination)) continue;
        File.Copy(source, destination, true);
    }

    string manifestPath = Path.Combine(output, "NovaOrynProject.json");
    if (!File.Exists(manifestPath)) File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
    {
        Name = "MinimalKernel",
        ProjectFile = "NovaOrynKernel.csproj",
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
