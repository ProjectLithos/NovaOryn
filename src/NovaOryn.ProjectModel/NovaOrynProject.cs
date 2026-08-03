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
        if (!File.Exists(path)) { error = $"Project manifest not found: {path}"; return false; }
        try
        {
            project = JsonSerializer.Deserialize<NovaOrynProject>(File.ReadAllText(path));
            if (project is null) { error = "Project manifest was empty."; return false; }
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
        if (string.IsNullOrWhiteSpace(ProjectFile)) { error = "ProjectFile is required."; return false; }
        if (!string.Equals(TargetArchitecture, "x64", StringComparison.OrdinalIgnoreCase)) { error = "0.0.3 supports x64 only."; return false; }
        if (!string.Equals(KernelEntry, "KMain", StringComparison.Ordinal)) { error = "KernelEntry must be KMain."; return false; }
        error = string.Empty;
        return true;
    }
}
