using System.Diagnostics;
using System.Text.Json;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "link", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.Linker link <NovaOrynProject.json> --lld-link <path> --llvm-nm <path> [--native-root <path>] [--dry-run]");
    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string? lld = GetOption(args, "--lld-link") ?? Environment.GetEnvironmentVariable("NOVAORYN_LLD_LINK");
    string? llvmNm = GetOption(args, "--llvm-nm") ?? Environment.GetEnvironmentVariable("NOVAORYN_LLVM_NM");
    if (string.IsNullOrWhiteSpace(lld) || string.IsNullOrWhiteSpace(llvmNm)) return Fail("Both lld-link and llvm-nm paths are required.");
    string output = project.OutputDirectory;
    string compileManifest = Path.Combine(output, "NovaOryn.Compile.json");
    if (!File.Exists(compileManifest)) return Fail($"Compilation manifest not found: {compileManifest}");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(compileManifest));
    string nativeLibrary = document.RootElement.GetProperty("nativeLibrary").GetString() ?? string.Empty;
    if (!File.Exists(nativeLibrary)) return Fail($"NativeAOT library not found: {nativeLibrary}");

    Console.WriteLine($"[INFO] Verifying that the ILC output exports NovaOrynManagedEntry for managed KMain.");
    if (!HasOption(args, "--dry-run"))
    {
        (int nmExit, string nmOutput) = Capture(llvmNm, ["--defined-only", nativeLibrary]);
        if (nmExit != 0) return Fail($"llvm-nm failed with exit code {nmExit}.");
        if (!nmOutput.Contains("NovaOrynManagedEntry", StringComparison.Ordinal))
            return Fail("The NativeAOT library does not export NovaOrynManagedEntry, which is the native bridge to KMain.");
    }

    string nativeRoot = Path.GetFullPath(GetOption(args, "--native-root") ?? Path.Combine(AppContext.BaseDirectory, "native", "x64"));
    string entryObject = Path.Combine(nativeRoot, "Entry.obj");
    string cpuObject = Path.Combine(nativeRoot, "Cpu.obj");
    string efi = Path.Combine(output, project.Name + ".efi");
    string map = Path.Combine(output, project.Name + ".map");
    string[] linkArguments =
    [
        "/nologo", "/subsystem:efi_application", "/machine:x64", "/nodefaultlib", "/entry:NovaOrynUefiEntry",
        $"/out:{efi}", $"/map:{map}", entryObject, cpuObject, nativeLibrary
    ];
    Console.WriteLine($"[INFO] {lld} {string.Join(" ", linkArguments.Select(Quote))}");
    if (HasOption(args, "--dry-run")) return 0;
    if (!File.Exists(entryObject) || !File.Exists(cpuObject)) return Fail("Native entry objects are missing. Run Build-NovaOryn before linking.");
    int exitCode = Run(lld, linkArguments);
    if (exitCode != 0) return Fail($"LLD failed with exit code {exitCode}.");
    Console.WriteLine($"[ OK ] EFI application linked: {efi}");
    return 0;
}

static int Run(string executable, IEnumerable<string> arguments) { using Process p = new(); p.StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false }; foreach (string a in arguments) p.StartInfo.ArgumentList.Add(a); if (!p.Start()) return -1; p.WaitForExit(); return p.ExitCode; }
static (int ExitCode, string Output) Capture(string executable, IEnumerable<string> arguments) { using Process p = new(); p.StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }; foreach (string a in arguments) p.StartInfo.ArgumentList.Add(a); p.Start(); string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(); return (p.ExitCode, output); }
static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
