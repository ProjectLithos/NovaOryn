using System.Text.RegularExpressions;

string root = FindRepositoryRoot(AppContext.BaseDirectory);
List<string> failures = [];
foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
{
    string source = File.ReadAllText(file);
    if (source.Contains("public void ", StringComparison.Ordinal) || source.Contains("public static void ", StringComparison.Ordinal))
        failures.Add($"Public void method found: {Path.GetRelativePath(root, file)}");
}
string kernel = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Sample", "Kernel.cs"));
if (!Regex.IsMatch(kernel, @"public\s+static\s+bool\s+KMain\s*\(")) failures.Add("KMain must be public static bool.");
string cpu = File.ReadAllText(Path.Combine(root, "native", "x64", "Cpu.S"));
if (!cpu.Contains("cli", StringComparison.Ordinal) || !cpu.Contains("hlt", StringComparison.Ordinal) || !cpu.Contains("jmp .LNovaOrynHaltForever", StringComparison.Ordinal)) failures.Add("Native halt loop is incomplete.");

string solution = File.ReadAllText(Path.Combine(root, "NovaOryn.sln"));
if (!solution.Contains("Release|Any CPU", StringComparison.Ordinal)) failures.Add("Solution must define Release|Any CPU.");
string buildScript = File.ReadAllText(Path.Combine(root, "Build-NovaOryn.ps1"));
if (!buildScript.Contains("--property:Platform=\"Any CPU\"", StringComparison.Ordinal)) failures.Add("Build script must explicitly select Any CPU for managed solution projects.");
string projectManifest = File.ReadAllText(Path.Combine(root, "examples", "MinimalKernel", "NovaOrynProject.json"));
if (!projectManifest.Contains("../../src/NovaOryn.Kernel.Sample", StringComparison.Ordinal)) failures.Add("Minimal kernel manifest must resolve the sample project from its own directory.");
string bootstrapProject = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "NovaOryn.Kernel.Bootstrap.csproj"));
if (!bootstrapProject.Contains("<ImplicitUsings>disable</ImplicitUsings>", StringComparison.Ordinal)) failures.Add("Freestanding bootstrap must disable generated global usings.");
if (!bootstrapProject.Contains("<Nullable>disable</Nullable>", StringComparison.Ordinal)) failures.Add("Freestanding bootstrap must disable nullable metadata generation.");

if (failures.Count != 0)
{
    foreach (string failure in failures) Console.Error.WriteLine($"[FAIL] {failure}");
    return 1;
}
Console.WriteLine("[ OK ] Public C# APIs contain no public void methods.");
Console.WriteLine("[ OK ] Kernel entry is KMain and returns bool.");
Console.WriteLine("[ OK ] x64 halt executes CLI and a repeating HLT loop.");
return 0;

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("NovaOryn repository root was not found.");
}
