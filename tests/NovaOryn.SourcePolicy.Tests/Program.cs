using System.Text.RegularExpressions;

string root = FindRepositoryRoot(AppContext.BaseDirectory);
List<string> failures = [];
foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
{
    string source = File.ReadAllText(file);
    if (source.Contains("public void ", StringComparison.Ordinal) || source.Contains("public static void ", StringComparison.Ordinal))
    {
        failures.Add($"Public void method found: {Path.GetRelativePath(root, file)}");
    }
}

string kernel = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Sample", "Kernel.cs"));
if (!Regex.IsMatch(kernel, @"public\s+static\s+bool\s+KMain\s*\("))
{
    failures.Add("KMain must be public static bool.");
}
string cpu = File.ReadAllText(Path.Combine(root, "native", "x64", "Cpu.S"));
if (!cpu.Contains("cli", StringComparison.Ordinal) || !cpu.Contains("hlt", StringComparison.Ordinal) || !cpu.Contains("jmp .LNovaOrynHaltForever", StringComparison.Ordinal))
{
    failures.Add("Native halt loop is incomplete.");
}

string solution = File.ReadAllText(Path.Combine(root, "NovaOryn.sln"));
if (!solution.Contains("Release|Any CPU", StringComparison.Ordinal))
{
    failures.Add("Solution must define Release|Any CPU.");
}
string buildScript = File.ReadAllText(Path.Combine(root, "Build-NovaOryn.ps1"));
if (!buildScript.Contains("--property:Platform=\"Any CPU\"", StringComparison.Ordinal))
{
    failures.Add("Build script must explicitly select Any CPU for managed solution projects.");
}
if (!buildScript.Contains("--ilc $ilc", StringComparison.Ordinal))
{
    failures.Add("Build script must pass the repository-pinned ILC executable to NovaOryn.ManagedCompiler.");
}
if (!buildScript.Contains("NovaOryn.SourcePolicy.Tests", StringComparison.Ordinal))
{
    failures.Add("Build script must execute the source-policy tests.");
}

string projectManifest = File.ReadAllText(Path.Combine(root, "examples", "MinimalKernel", "NovaOrynProject.json"));
if (!projectManifest.Contains("../../src/NovaOryn.Kernel.Bootstrap", StringComparison.Ordinal))
{
    failures.Add("Minimal kernel manifest must select the NovaOryn bootstrap system module.");
}
string bootstrapProject = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "NovaOryn.Kernel.Bootstrap.csproj"));
if (!bootstrapProject.Contains("<ImplicitUsings>disable</ImplicitUsings>", StringComparison.Ordinal))
{
    failures.Add("Freestanding bootstrap must disable generated global usings.");
}
if (!bootstrapProject.Contains("<Nullable>disable</Nullable>", StringComparison.Ordinal))
{
    failures.Add("Freestanding bootstrap must disable nullable metadata generation.");
}
foreach (string forbidden in new[] { "<PublishAot>", "<RuntimeIdentifier>", "<SelfContained>", "<NativeLib>" })
{
    if (bootstrapProject.Contains(forbidden, StringComparison.Ordinal))
    {
        failures.Add($"Freestanding bootstrap project must compile to IL only and must not contain {forbidden}.");
    }
}

string bootstrapCoreLib = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Kernel.Bootstrap", "CoreLib.cs"));
if (!bootstrapCoreLib.Contains("#pragma warning disable CS0169", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("private IntPtr _methodTable;", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("#pragma warning restore CS0169", StringComparison.Ordinal))
{
    failures.Add("Freestanding Object must retain its NativeAOT method-table field with a narrowly scoped CS0169 suppression.");
}
if (!bootstrapCoreLib.Contains("internal class Array<T> : Array", StringComparison.Ordinal))
{
    failures.Add("Freestanding CoreLib must provide the compiler-required generic array type.");
}
if (!bootstrapCoreLib.Contains("public static class Buffer", StringComparison.Ordinal) ||
    !bootstrapCoreLib.Contains("BulkMoveWithWriteBarrier", StringComparison.Ordinal))
{
    failures.Add("Freestanding CoreLib must provide the .NET 10 ILC System.Buffer helper contract.");
}

string managedCompiler = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.ManagedCompiler", "Program.cs"));
if (managedCompiler.Contains("\"publish\"", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.ManagedCompiler must not use dotnet publish for the no-CoreLib bootstrap.");
}
if (managedCompiler.Contains("\"-O\"", StringComparison.Ordinal))
{
    failures.Add("No-GC bootstrap ILC compilation must not enable -O because it implies the IL scanner.");
}
foreach (string required in new[] { "--systemmodule", "--targetos:win", "--targetarch:x64", "--nativelib", "--directpinvoke:*", "--noscan", "--reflectiondata:none", "--nopreinitstatics" })
{
    if (!managedCompiler.Contains(required, StringComparison.Ordinal))
    {
        failures.Add($"Direct ILC invocation is missing {required}.");
    }
}
if (!managedCompiler.Contains("nativeObject", StringComparison.Ordinal))
{
    failures.Add("Compilation manifest must record the direct ILC native object.");
}
if (!managedCompiler.Contains("schemaVersion = 5", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.ManagedCompiler must emit compilation manifest schema 5.");
}

string linker = File.ReadAllText(Path.Combine(root, "src", "NovaOryn.Linker", "Program.cs"));
if (!linker.Contains("GetProperty(\"nativeObject\")", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.Linker must consume the direct ILC native object.");
}
if (linker.Contains("nativeLibrary", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.Linker must not expect the obsolete NativeAOT static library output.");
}
if (!linker.Contains("SupportedCompilationManifestSchema = 5", StringComparison.Ordinal))
{
    failures.Add("NovaOryn.Linker must accept compilation manifest schema 5.");
}

if (failures.Count != 0)
{
    foreach (string failure in failures)
    {
        Console.Error.WriteLine($"[FAIL] {failure}");
    }
    return 1;
}
Console.WriteLine("[ OK ] Public C# APIs contain no public void methods.");
Console.WriteLine("[ OK ] Kernel entry is KMain and returns bool.");
Console.WriteLine("[ OK ] x64 halt executes CLI and a repeating HLT loop.");
Console.WriteLine("[ OK ] No-CoreLib kernel compilation invokes ILC directly.");
Console.WriteLine("[ OK ] Windows NativeAOT runtime-pack resolution is not used.");
return 0;

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("NovaOryn repository root was not found.");
}
