namespace NovaOryn.DocumentationGenerator;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("NovaOryn.DocumentationGenerator generate [--root <repository>] [--configuration <file>] [--validate]");
    return 0;
}
if (!string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unknown command: {args[0]}");
    return 2;
}

string root = FindRepositoryRoot(GetOption(args, "--root") ?? Directory.GetCurrentDirectory());
string configPath = GetOption(args, "--configuration") ?? Path.Combine(root, "docs", "NovaOryn.Documentation.json");
bool validate = args.Contains("--validate", StringComparer.OrdinalIgnoreCase);
DocumentationConfiguration configuration = ConfigurationReader.Read(Path.GetFullPath(configPath));
IReadOnlyList<ProjectDocumentation> projects = PublicApiCollector.Collect(root, configuration);
List<string> failures = [];
if (validate && configuration.RequireDocumentationForPublicItems)
{
    foreach (ApiDocumentation item in projects.Where(project => project.IsPublicAssembly).SelectMany(project => project.Items))
    {
        if (item.Summary.Length == 0) failures.Add($"Missing summary: {item.Assembly}::{item.QualifiedName}");
        if (item.WhenToUse.Length == 0) failures.Add($"Missing <nova.when>: {item.Assembly}::{item.QualifiedName}");
        if (item.Dependencies.Length == 0) failures.Add($"Missing dependency information: {item.Assembly}::{item.QualifiedName}");
        if (configuration.RequireExampleForPublicMethods && item.Kind == "Method" && item.Example.Length == 0)
            failures.Add($"Missing example: {item.Assembly}::{item.QualifiedName}");
    }
}
HtmlSiteWriter.Write(root, configuration, projects);
Console.WriteLine($"[ OK ] Generated NovaOryn SDK usage site with {projects.Count} assemblies and {projects.Sum(project => project.Items.Count)} public items.");
if (failures.Count == 0) return 0;
foreach (string failure in failures) Console.Error.WriteLine($"[FAIL] {failure}");
return 1;

static string? GetOption(string[] arguments, string name)
{
    int index = Array.FindIndex(arguments, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(Path.GetFullPath(start));
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("NovaOryn repository root was not found.");
}
