using System;
using System.IO;
namespace NovaOryn.VisualStudio;
internal static class NovaOrynProjectRecognizer
{
    public static bool IsNovaOrynProject(string projectFile)
    {
        if (string.IsNullOrWhiteSpace(projectFile) || !File.Exists(projectFile)) return false;
        string directory = Path.GetDirectoryName(projectFile) ?? string.Empty;
        if (File.Exists(Path.Combine(directory, "NovaOrynProject.json"))) return true;
        string text = File.ReadAllText(projectFile);
        return text.IndexOf("<NovaOrynProject>true</NovaOrynProject>", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
