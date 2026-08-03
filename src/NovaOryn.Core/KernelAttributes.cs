namespace NovaOryn.Core;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class KernelEntryAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class DoesNotReturnAttribute : Attribute;

public readonly record struct VersionInfo(ushort Major, ushort Minor, ushort Patch)
{
    public static VersionInfo Current => new(0, 0, 1);
}
