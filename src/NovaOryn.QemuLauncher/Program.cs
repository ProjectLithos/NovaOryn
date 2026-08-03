using System.Diagnostics;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.QemuLauncher run <NovaOrynProject.json> --qemu <path> --image <path> [--dry-run]");
    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string? qemu = GetOption(args, "--qemu") ?? Environment.GetEnvironmentVariable("NOVAORYN_QEMU_X64");
    string? image = GetOption(args, "--image");
    if (string.IsNullOrWhiteSpace(qemu) || string.IsNullOrWhiteSpace(image)) return Fail("QEMU and image paths are required.");
    string arguments = $"-machine q35 -cpu max -m 512M -serial file:"{Path.GetFullPath(project.OutputDirectory)}/serial.log" -monitor none -drive if=none,format=raw,file="{Path.GetFullPath(image)}",id=boot -device ide-hd,drive=boot";
    // Deliberately no -S: QEMU must not start paused.
    Console.WriteLine($"[INFO] {qemu} {arguments}");
    if (Array.Exists(args, value => string.Equals(value, "--dry-run", StringComparison.OrdinalIgnoreCase))) return 0;
    if (!File.Exists(qemu)) return Fail($"QEMU executable not found: {qemu}");
    using Process process = Process.Start(new ProcessStartInfo(qemu, arguments) { UseShellExecute = false }) ?? throw new InvalidOperationException("QEMU failed to start.");
    process.WaitForExit();
    return process.ExitCode;
}
static string? GetOption(string[] args, string name) { for (int i=0;i+1<args.Length;i++) if (string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase)) return args[i+1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
