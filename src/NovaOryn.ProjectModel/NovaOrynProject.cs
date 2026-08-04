using System.Text.Json;

namespace NovaOryn.ProjectModel;

public sealed record NovaOrynProject(
    string Name,
    string ProjectFile,
    string TargetArchitecture,
    string BootProtocol,
    string KernelEntry,
    string RuntimePack,
    string OutputDirectory)
{
    public static bool TryLoad(string path, out NovaOrynProject? project, out string error)
    {
        project = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Project path is required.", nameof(path));
        string manifestPath = Path.GetFullPath(path);
        if (!File.Exists(manifestPath)) { error = $"Project manifest not found: {manifestPath}"; return false; }
        try
        {
            project = JsonSerializer.Deserialize<NovaOrynProject>(File.ReadAllText(manifestPath));
            if (project is null) { error = "Project manifest was empty."; return false; }
            string root = Path.GetDirectoryName(manifestPath) ?? Environment.CurrentDirectory;
            project = project with
            {
                ProjectFile = Resolve(root, project.ProjectFile),
                OutputDirectory = Resolve(root, project.OutputDirectory)
            };
            return project.Validate(out error);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(Name)) { error = "Name is required."; return false; }
        if (!File.Exists(ProjectFile)) { error = $"Kernel project was not found: {ProjectFile}"; return false; }
        if (!string.Equals(TargetArchitecture, "x64", StringComparison.OrdinalIgnoreCase)) { error = "NovaOryn 0.0.18 supports x64 only."; return false; }
        if (!string.Equals(BootProtocol, "Uefi", StringComparison.OrdinalIgnoreCase)) { error = "NovaOryn 0.0.18 supports UEFI only."; return false; }
        if (!string.Equals(KernelEntry, "KMain", StringComparison.Ordinal)) { error = "KernelEntry must be KMain."; return false; }
        error = string.Empty;
        return true;
    }

    private static string Resolve(string root, string value) => Path.IsPathRooted(value) ? Path.GetFullPath(value) : Path.GetFullPath(Path.Combine(root, value));
}
