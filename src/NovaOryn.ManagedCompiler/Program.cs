using System.Diagnostics;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "compile", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.ManagedCompiler compile <NovaOrynProject.json> [--ilc <path>] [--dry-run]");

    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null)
        return Fail(error);

    string? ilc = GetOption(args, "--ilc") ?? Environment.GetEnvironmentVariable("NOVAORYN_ILC");
    bool dryRun = Array.Exists(args, value => string.Equals(value, "--dry-run", StringComparison.OrdinalIgnoreCase));
    if (string.IsNullOrWhiteSpace(ilc))
        return Fail("ILC path is required through --ilc or NOVAORYN_ILC.");

    string output = Path.GetFullPath(project.OutputDirectory);
    Directory.CreateDirectory(output);
    string responseFile = Path.Combine(output, "ilc.rsp");
    string[] lines =
    [
        "# NovaOryn 0.0.3 generated ILC plan",
        $"--out:{Path.Combine(output, project.Name + ".obj")}",
        $"--targetarch:{project.TargetArchitecture}",
        $"--entrypoint:{project.KernelEntry}",
        $"# project:{Path.GetFullPath(project.ProjectFile)}",
        $"# runtime-pack:{project.RuntimePack}"
    ];
    File.WriteAllLines(responseFile, lines);
    Console.WriteLine($"[ OK ] ILC response plan: {responseFile}");
    if (dryRun) return 0;
    if (!File.Exists(ilc)) return Fail($"ILC executable not found: {ilc}");

    using Process process = new();
    process.StartInfo = new ProcessStartInfo(ilc, $"@"{responseFile}"")
    {
        UseShellExecute = false
    };
    if (!process.Start()) return Fail("ILC process did not start.");
    process.WaitForExit();
    return process.ExitCode;
}

static string? GetOption(string[] args, string name)
{
    for (int index = 0; index + 1 < args.Length; index++)
        if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
    return null;
}

static int Fail(string message)
{
    Console.Error.WriteLine($"[FAIL] {message}");
    return 1;
}
