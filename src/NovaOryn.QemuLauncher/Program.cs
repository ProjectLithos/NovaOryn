using System.Diagnostics;
using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.QemuLauncher run <NovaOrynProject.json> --qemu <path> --image <path> [--dry-run]");

    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null)
        return Fail(error);

    string? qemu = GetOption(args, "--qemu") ?? Environment.GetEnvironmentVariable("NOVAORYN_QEMU_X64");
    string? image = GetOption(args, "--image");
    if (string.IsNullOrWhiteSpace(qemu) || string.IsNullOrWhiteSpace(image))
        return Fail("QEMU and image paths are required.");

    string outputDirectory = Path.GetFullPath(project.OutputDirectory);
    string imagePath = Path.GetFullPath(image);
    string serialLog = Path.Combine(outputDirectory, "serial.log");
    Directory.CreateDirectory(outputDirectory);

    string[] qemuArguments =
    [
        "-machine", "q35",
        "-cpu", "max",
        "-m", "512M",
        "-serial", $"file:{serialLog}",
        "-monitor", "none",
        "-drive", $"if=none,format=raw,file={imagePath},id=boot",
        "-device", "ide-hd,drive=boot"
    ];

    Console.WriteLine($"[INFO] {qemu} {string.Join(" ", qemuArguments.Select(Quote))}");
    if (HasOption(args, "--dry-run")) return 0;
    if (!File.Exists(qemu)) return Fail($"QEMU executable not found: {qemu}");
    if (!File.Exists(imagePath)) return Fail($"Boot image not found: {imagePath}");

    using Process process = new();
    process.StartInfo = new ProcessStartInfo(qemu) { UseShellExecute = false };
    foreach (string argument in qemuArguments) process.StartInfo.ArgumentList.Add(argument);
    if (!process.Start()) return Fail("QEMU failed to start.");
    process.WaitForExit();
    return process.ExitCode;
}

static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
static bool HasOption(string[] args, string option) => args.Any(value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
static string? GetOption(string[] args, string name) { for (int i = 0; i + 1 < args.Length; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1]; return null; }
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
