namespace System
{
    public class Object
    {
#pragma warning disable CS0169 // The NativeAOT object header is consumed by generated code, not referenced by C# source.
        private IntPtr _methodTable;
#pragma warning restore CS0169
    }
    public struct Void { }
    public struct Boolean { }
    public struct Char { }
    public struct SByte { }
    public struct Byte { }
    public struct Int16 { }
    public struct UInt16 { }
    public struct Int32 { }
    public struct UInt32 { }
    public struct Int64 { }
    public struct UInt64 { }
    public struct IntPtr { }
    public struct UIntPtr { }
    public struct Single { }
    public struct Double { }
    public abstract class ValueType { }
    public abstract class Enum : ValueType { }
    public abstract class Array { }
    internal class Array<T> : Array { }

    // .NET 10 ILC recognizes this CoreLib type as the bulk-reference-copy helper owner.
    // The bootstrap has no GC and must never perform a managed-reference bulk copy.
    public static class Buffer
    {
        internal static void BulkMoveWithWriteBarrier(ref Byte destination, ref Byte source, UIntPtr byteCount)
        {
            while (true) { }
        }
    }
    public sealed class String { public readonly Int32 Length; public Char this[Int32 index] { get { while (true) { } } } }
    public abstract class Delegate { }
    public abstract class MulticastDelegate : Delegate { }
    public class Attribute { }
    public enum AttributeTargets { }
    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets targets) { }
        public Boolean AllowMultiple { get; set; }
        public Boolean Inherited { get; set; }
    }
    public struct RuntimeTypeHandle { }
    public struct RuntimeMethodHandle { }
    public struct RuntimeFieldHandle { }
    public struct Nullable<T> where T : struct { }

    namespace Reflection
    {
        public sealed class DefaultMemberAttribute : Attribute
        {
            public DefaultMemberAttribute(String memberName) { }
        }
    }

    namespace Runtime
    {
        public sealed class RuntimeExportAttribute : Attribute
        {
            public RuntimeExportAttribute(String name) { }
        }
    }

    namespace Runtime.CompilerServices
    {
        public sealed class CompilerGeneratedAttribute : Attribute { }
        public sealed class IsReadOnlyAttribute : Attribute { }
        public sealed class IsByRefLikeAttribute : Attribute { }
        public static class RuntimeFeature
        {
            public const String UnmanagedSignatureCallingConvention = nameof(UnmanagedSignatureCallingConvention);
        }
        public static class RuntimeHelpers
        {
            public static unsafe Int32 OffsetToStringData => sizeof(IntPtr) + sizeof(Int32);
        }
    }

    namespace Runtime.InteropServices
    {
        public enum CallingConvention { Winapi = 1, Cdecl = 2, StdCall = 3, ThisCall = 4, FastCall = 5 }
        public enum CharSet { None = 1, Ansi = 2, Unicode = 3, Auto = 4 }
        public enum LayoutKind { Sequential = 0, Explicit = 2, Auto = 3 }
        public sealed class StructLayoutAttribute : Attribute
        {
            public StructLayoutAttribute(LayoutKind kind) { }
        }
        public sealed class DllImportAttribute : Attribute
        {
            public DllImportAttribute(String libraryName) { }
            public String EntryPoint { get; set; }
            public CallingConvention CallingConvention { get; set; }
            public Boolean ExactSpelling { get; set; }
        }
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    using System;
    using System.Runtime;

    internal static class StartupCodeHelpers
    {
        [RuntimeExport("RhpReversePInvoke")]
        private static void RhpReversePInvoke(IntPtr frame) { }

        [RuntimeExport("RhpReversePInvokeReturn")]
        private static void RhpReversePInvokeReturn(IntPtr frame) { }

        [RuntimeExport("RhpPInvoke")]
        private static void RhpPInvoke(IntPtr frame) { }

        [RuntimeExport("RhpPInvokeReturn")]
        private static void RhpPInvokeReturn(IntPtr frame) { }

        [RuntimeExport("RhpFallbackFailFast")]
        private static void RhpFallbackFailFast()
        {
            while (true) { }
        }
    }
}
