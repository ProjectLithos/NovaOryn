using NovaOryn.ProjectModel;

return MainEntry(args);

static int MainEntry(string[] args)
{
    if (args.Length < 2 || !string.Equals(args[0], "create", StringComparison.OrdinalIgnoreCase))
        return Fail("Usage: NovaOryn.ImageBuilder create <NovaOrynProject.json>");
    if (!NovaOrynProject.TryLoad(args[1], out NovaOrynProject? project, out string error) || project is null) return Fail(error);
    string output = Path.GetFullPath(project.OutputDirectory);
    Directory.CreateDirectory(output);
    string plan = Path.Combine(output, "image-plan.txt");
    File.WriteAllLines(plan,
    [
        "NovaOryn image plan 0.0.3",
        "PartitionTable=GPT",
        "FileSystem=FAT32",
        "EfiPath=EFI/BOOT/BOOTX64.EFI",
        $"Kernel={project.Name}.elf",
        "Status=FoundationOnly"
    ]);
    Console.WriteLine($"[ OK ] Image plan: {plan}");
    Console.WriteLine("[WARN] 0.0.3 does not yet emit a bootable FAT32 image.");
    return 0;
}
static int Fail(string message) { Console.Error.WriteLine($"[FAIL] {message}"); return 1; }
