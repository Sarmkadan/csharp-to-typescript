#if !NET5_0_OR_GREATER

// Lets records and init-only setters compile for netstandard2.0. The runtime type is only needed by the compiler.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

#endif
