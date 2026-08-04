using System.Diagnostics;
using System.Text.Json;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "link", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.Linker link <NovaOrynProject.json> --lld-link <path> --llvm-nm <path> --nasm <path> [--native-root <path>] [--dry-run]");

    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string? lld = GetOption(args, "--lld-link");
    string? llvmNm = GetOption(args, "--llvm-nm");
    if (string.IsNullOrWhiteSpace(lld) || string.IsNullOrWhiteSpace(llvmNm)) return Fail("lld-link and llvm-nm are required.");
    string nativeRoot = GetOption(args, "--native-root") ?? Path.Combine(Environment.CurrentDirectory, "Artifacts", "Native", "x64");
    bool dryRun = HasOption(args, "--dry-run");

    string manifestPath = Path.Combine(project.OutputDirectory, "NovaOryn.Compile.json");
    if (!File.Exists(manifestPath)) return Fail($"Compilation manifest not found: {manifestPath}");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
    string runtimeMode = document.RootElement.GetProperty("runtimeMode").GetString() ?? string.Empty;
    if (!string.Equals(runtimeMode, "NoGcBootstrap", StringComparison.Ordinal)) return Fail($"Unsupported runtime mode: {runtimeMode}");
    string nativeLibrary = document.RootElement.GetProperty("nativeLibrary").GetString() ?? string.Empty;
    if (!File.Exists(nativeLibrary)) return Fail($"Native library not found: {nativeLibrary}");

    ProcessResult symbols = Capture(llvmNm, ["--defined-only", nativeLibrary]);
    if (symbols.ExitCode != 0 || !symbols.Output.Contains("NovaOrynManagedEntry", StringComparison.Ordinal))
        return Fail("ILC output does not export NovaOrynManagedEntry for KMain.");

    string entry = Path.Combine(nativeRoot, "Entry.obj");
    string cpu = Path.Combine(nativeRoot, "Cpu.obj");
    string runtime = Path.Combine(nativeRoot, "Runtime.obj");
    foreach (string file in new[] { entry, cpu, runtime }) if (!File.Exists(file)) return Fail($"Native object not found: {file}");

    string output = Path.Combine(project.OutputDirectory, project.Name + ".efi");
    string map = Path.Combine(project.OutputDirectory, project.Name + ".map");
    string[] linkArguments =
    [
        "/nologo", "/subsystem:efi_application", "/machine:x64", "/nodefaultlib",
        "/entry:NovaOrynUefiEntry", "/errorlimit:64", $"/out:{output}", $"/map:{map}",
        entry, cpu, runtime, nativeLibrary
    ];

    Console.WriteLine("[INFO] Linking only NovaOryn native objects and the NovaOryn bootstrap ILC library.");
    Console.WriteLine("[INFO] Windows NativeAOT runtime libraries linked: 0");
    if (dryRun) return 0;
    ProcessResult result = Capture(lld, linkArguments);
    Console.Write(result.Output);
    if (result.ExitCode != 0) return Fail($"LLD failed with exit code {result.ExitCode}.");
    Console.WriteLine($"[ OK ] Freestanding EFI application: {output}");
    Console.WriteLine($"[ OK ] Link map: {map}");
    return 0;
}

static ProcessResult Capture(string executable, IEnumerable<string> arguments)
{
    using Process process = new();
    process.StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    if (!process.Start()) return new ProcessResult(-1, string.Empty);
    string stdout = process.StandardOutput.ReadToEnd();
    string stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return new ProcessResult(process.ExitCode, stdout + stderr);
}
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
readonly record struct ProcessResult(int ExitCode, string Output);
