using System.Diagnostics;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "link", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.Linker link <NovaOrynProject.json> --lld <path> [--dry-run]");
    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string? lld = GetOption(args, "--lld") ?? Environment.GetEnvironmentVariable("NOVAORYN_LLD");
    if (string.IsNullOrWhiteSpace(lld)) return Fail("LLD path is required through --lld or NOVAORYN_LLD.");
    string output = Path.GetFullPath(project.OutputDirectory);
    Directory.CreateDirectory(output);
    string elf = Path.Combine(output, project.Name + ".elf");
    string objectFile = Path.Combine(output, project.Name + ".obj");
    string nativeRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "native", "x64"));
    string arguments = $"-nostdlib -static -T "{Path.Combine(nativeRoot, "kernel.ld")}" -o "{elf}" "{Path.Combine(nativeRoot, "Entry.o")}" "{Path.Combine(nativeRoot, "Cpu.o")}" "{objectFile}"";
    Console.WriteLine($"[INFO] {lld} {arguments}");
    if (Array.Exists(args, value => string.Equals(value, "--dry-run", StringComparison.OrdinalIgnoreCase))) return 0;
    if (!File.Exists(lld)) return Fail($"LLD executable not found: {lld}");
    using Process process = Process.Start(new ProcessStartInfo(lld, arguments) { UseShellExecute = false }) ?? throw new InvalidOperationException("LLD failed to start.");
    process.WaitForExit();
    return process.ExitCode;
}
static string? GetOption(string[] args, string name) { for (int i=0;i+1<args.Length;i++) if (string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase)) return args[i+1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
