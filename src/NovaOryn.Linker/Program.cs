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
        ProcessResult nmResult = Capture(llvmNm, ["--defined-only", nativeLibrary], TimeSpan.FromSeconds(30));
        if (nmResult.TimedOut) return Fail("llvm-nm exceeded the 30-second validation timeout.");
        if (nmResult.ExitCode != 0) return Fail($"llvm-nm failed with exit code {nmResult.ExitCode}.");
        if (!nmResult.Output.Contains("NovaOrynManagedEntry", StringComparison.Ordinal))
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

    if (HasOption(args, "--dry-run"))
    {
        string[] dryArguments = CreateLinkArguments(efi, map, [entryObject, cpuObject, runtimeObject, nativeLibrary]);
        Console.WriteLine($"[INFO] {lld} {string.Join(" ", dryArguments.Select(Quote))}");
        return 0;
    }

    HashSet<string> platformImports = new(StringComparer.Ordinal);
    const int maximumPasses = 32;

    for (int pass = 1; pass <= maximumPasses; pass++)
    {
        List<string> inputs = [entryObject, cpuObject, runtimeObject];
        if (platformImports.Count != 0)
        {
            WritePlatformStubAssembly(shimSource, platformImports.OrderBy(value => value, StringComparer.Ordinal));
            int nasmExit = Run(nasm, ["-f", "win64", shimSource, "-o", shimObject], TimeSpan.FromSeconds(30));
            if (nasmExit != 0) return Fail($"NASM failed while generating NativeAOT platform stubs with exit code {nasmExit}.");
            inputs.Add(shimObject);
        }
        inputs.Add(nativeLibrary);

        string[] linkArguments = CreateLinkArguments(efi, map, inputs);
        Console.WriteLine($"[INFO] Native link pass {pass}/{maximumPasses} with {platformImports.Count} compatibility stubs.");
        ProcessResult linkResult = Capture(lld, linkArguments, TimeSpan.FromMinutes(2));
        if (linkResult.TimedOut)
            return Fail("LLD exceeded the two-minute link timeout. The linker process was terminated instead of leaving the build hung.");

        if (linkResult.ExitCode == 0)
        {
            string report = Path.Combine(output, "NovaOryn.NativeAot.PlatformImports.txt");
            File.WriteAllLines(report, platformImports.OrderBy(value => value, StringComparer.Ordinal));
            Console.WriteLine($"[ OK ] EFI application linked: {efi}");
            Console.WriteLine($"[ OK ] Platform import report: {report}");
            return 0;
        }

        string[] unresolved = ParseUndefinedSymbols(linkResult.Output);
        string[] forbidden = unresolved.Where(IsNovaOrynOrRuntimeContract).ToArray();
        if (forbidden.Length != 0)
        {
            Console.Error.WriteLine(linkResult.Output);
            return Fail($"Required NovaOryn/runtime symbols remain unresolved: {string.Join(", ", forbidden)}");
        }

        string[] newlyDiscovered = unresolved
            .Where(IsSupportedPlatformImport)
            .Where(platformImports.Add)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (newlyDiscovered.Length == 0)
        {
            Console.Error.WriteLine(linkResult.Output);
            return Fail($"LLD failed with exit code {linkResult.ExitCode}, but no additional supported platform imports were discovered.");
        }

        Console.WriteLine($"[WARN] Link pass {pass} discovered {newlyDiscovered.Length} additional stock-runtime host imports.");
    }

    return Fail($"Native linking did not converge after {maximumPasses} bounded passes.");
}

static string[] CreateLinkArguments(string output, string map, IEnumerable<string> inputs)
{
    List<string> arguments =
    [
        "/nologo", "/subsystem:efi_application", "/machine:x64", "/nodefaultlib", "/entry:NovaOrynUefiEntry",
        "/errorlimit:256", $"/out:{output}", $"/map:{map}"
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

static int Run(string executable, IEnumerable<string> arguments, TimeSpan timeout)
{
    ProcessResult result = Capture(executable, arguments, timeout);
    if (result.TimedOut) return -2;
    return result.ExitCode;
}

static ProcessResult Capture(string executable, IEnumerable<string> arguments, TimeSpan timeout)
{
    using Process process = new();
    process.StartInfo = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);

    StringBuilder output = new();
    process.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) output.AppendLine(eventArgs.Data); };
    process.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) output.AppendLine(eventArgs.Data); };

    if (!process.Start()) return new ProcessResult(-1, string.Empty, false);
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    bool exited = process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue));
    if (!exited)
    {
        try { process.Kill(entireProcessTree: true); } catch { }
        process.WaitForExit();
        return new ProcessResult(-2, output.ToString(), true);
    }

    process.WaitForExit();
    return new ProcessResult(process.ExitCode, output.ToString(), false);
}

static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }

readonly record struct ProcessResult(int ExitCode, string Output, bool TimedOut);
