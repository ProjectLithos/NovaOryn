using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "link", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.Linker link <NovaOrynProject.json> --lld-link <path> --llvm-nm <path> --nasm <path> [--native-root <path>] [--dry-run]");

    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null)
        return Fail(error);

    string? lld = GetOption(args, "--lld-link") ?? Environment.GetEnvironmentVariable("NOVAORYN_LLD_LINK");
    string? llvmNm = GetOption(args, "--llvm-nm") ?? Environment.GetEnvironmentVariable("NOVAORYN_LLVM_NM");
    string? nasm = GetOption(args, "--nasm") ?? Environment.GetEnvironmentVariable("NOVAORYN_NASM");
    if (string.IsNullOrWhiteSpace(lld) || string.IsNullOrWhiteSpace(llvmNm) || string.IsNullOrWhiteSpace(nasm))
        return Fail("lld-link, llvm-nm, and nasm paths are required.");

    string output = project.OutputDirectory;
    string compileManifest = Path.Combine(output, "NovaOryn.Compile.json");
    if (!File.Exists(compileManifest)) return Fail($"Compilation manifest not found: {compileManifest}");

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(compileManifest));
    string nativeLibrary = document.RootElement.GetProperty("nativeLibrary").GetString() ?? string.Empty;
    if (!File.Exists(nativeLibrary)) return Fail($"NativeAOT library not found: {nativeLibrary}");

    Console.WriteLine("[INFO] Verifying that the ILC output exports NovaOrynManagedEntry for managed KMain.");
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
    string runtimeObject = Path.Combine(nativeRoot, "Runtime.obj");
    string shimSource = Path.Combine(nativeRoot, "NativeAotPlatformStubs.generated.asm");
    string shimObject = Path.Combine(nativeRoot, "NativeAotPlatformStubs.generated.obj");
    string efi = Path.Combine(output, project.Name + ".efi");
    string map = Path.Combine(output, project.Name + ".map");

    if (!File.Exists(entryObject) || !File.Exists(cpuObject) || !File.Exists(runtimeObject))
        return Fail("Native entry or runtime objects are missing. Run Build-NovaOryn before linking.");

    List<string> baseInputs = [entryObject, cpuObject, runtimeObject, nativeLibrary];
    string[] firstArguments = CreateLinkArguments(efi, map, baseInputs);
    Console.WriteLine($"[INFO] {lld} {string.Join(" ", firstArguments.Select(Quote))}");
    if (HasOption(args, "--dry-run")) return 0;

    (int firstExit, string firstOutput) = Capture(lld, firstArguments);
    if (firstExit == 0)
    {
        Console.WriteLine($"[ OK ] EFI application linked: {efi}");
        return 0;
    }

    string[] unresolved = ParseUndefinedSymbols(firstOutput);
    string[] forbidden = unresolved.Where(IsNovaOrynOrRuntimeContract).ToArray();
    if (forbidden.Length != 0)
    {
        Console.Error.WriteLine(firstOutput);
        return Fail($"Required NovaOryn/runtime symbols remain unresolved: {string.Join(", ", forbidden)}");
    }

    string[] platformImports = unresolved.Where(IsSupportedPlatformImport).Distinct(StringComparer.Ordinal).Order().ToArray();
    if (platformImports.Length == 0)
    {
        Console.Error.WriteLine(firstOutput);
        return Fail($"LLD failed with exit code {firstExit}, and no supported platform imports could be isolated.");
    }

    WritePlatformStubAssembly(shimSource, platformImports);
    Console.WriteLine($"[WARN] The stock Windows NativeAOT pack requested {platformImports.Length} host imports.");
    Console.WriteLine("[INFO] Generating freestanding compatibility stubs for unreachable host-platform paths.");
    int nasmExit = Run(nasm, ["-f", "win64", shimSource, "-o", shimObject]);
    if (nasmExit != 0) return Fail($"NASM failed while generating NativeAOT platform stubs with exit code {nasmExit}.");

    List<string> finalInputs = [entryObject, cpuObject, runtimeObject, shimObject, nativeLibrary];
    string[] finalArguments = CreateLinkArguments(efi, map, finalInputs);
    (int finalExit, string finalOutput) = Capture(lld, finalArguments);
    if (finalExit != 0)
    {
        Console.Error.WriteLine(finalOutput);
        return Fail($"LLD failed after the freestanding platform-stub pass with exit code {finalExit}.");
    }

    File.WriteAllLines(Path.Combine(output, "NovaOryn.NativeAot.PlatformImports.txt"), platformImports);
    Console.WriteLine($"[ OK ] EFI application linked: {efi}");
    Console.WriteLine($"[ OK ] Platform import report: {Path.Combine(output, "NovaOryn.NativeAot.PlatformImports.txt")}");
    return 0;
}

static string[] CreateLinkArguments(string output, string map, IEnumerable<string> inputs)
{
    List<string> arguments =
    [
        "/nologo", "/subsystem:efi_application", "/machine:x64", "/nodefaultlib", "/entry:NovaOrynUefiEntry",
        "/errorlimit:0", $"/out:{output}", $"/map:{map}"
    ];
    arguments.AddRange(inputs);
    return arguments.ToArray();
}

static string[] ParseUndefinedSymbols(string text)
{
    return Regex.Matches(text, @"undefined symbol:\s*([^\r\n]+)", RegexOptions.CultureInvariant)
        .Cast<Match>()
        .Select(match => match.Groups[1].Value.Trim())
        .Where(value => value.Length != 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

static bool IsNovaOrynOrRuntimeContract(string symbol)
{
    return symbol.StartsWith("NovaOryn", StringComparison.Ordinal) ||
           symbol.StartsWith("Rhp", StringComparison.Ordinal) ||
           symbol.StartsWith("Rh", StringComparison.Ordinal) ||
           symbol.StartsWith("__managed", StringComparison.Ordinal);
}

static bool IsSupportedPlatformImport(string symbol)
{
    if (!Regex.IsMatch(symbol, @"^[A-Za-z_?$@.][A-Za-z0-9_?$@.]*$", RegexOptions.CultureInvariant)) return false;
    return !symbol.StartsWith("??", StringComparison.Ordinal);
}

static void WritePlatformStubAssembly(string path, IEnumerable<string> symbols)
{
    StringBuilder source = new();
    source.AppendLine("bits 64");
    source.AppendLine("default rel");
    source.AppendLine("section .text");
    source.AppendLine();
    foreach (string symbol in symbols)
    {
        source.Append("global ").AppendLine(symbol);
        source.Append(symbol).AppendLine(":");
        source.AppendLine("    xor eax, eax");
        source.AppendLine("    ret");
        source.AppendLine();
    }
    File.WriteAllText(path, source.ToString(), new UTF8Encoding(false));
}

static int Run(string executable, IEnumerable<string> arguments)
{
    using Process process = new();
    process.StartInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
    foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    if (!process.Start()) return -1;
    process.WaitForExit();
    return process.ExitCode;
}

static (int ExitCode, string Output) Capture(string executable, IEnumerable<string> arguments)
{
    using Process process = new();
    process.StartInfo = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
    process.Start();
    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, output);
}

static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
