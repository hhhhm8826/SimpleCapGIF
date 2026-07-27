using System.Runtime.InteropServices;

namespace SimpleCapGIF.Windows.Capture;

internal static class D3DShaderCompiler
{
    private const uint CompileEnableStrictness = 1u << 11;
    private const uint CompileOptimizationLevel3 = 1u << 15;

    internal static byte[] Compile(string source, string entryPoint, string target)
    {
        var sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
        var result = D3DCompile(
            sourceBytes,
            (nuint)sourceBytes.Length,
            null,
            0,
            0,
            entryPoint,
            target,
            CompileEnableStrictness | CompileOptimizationLevel3,
            0,
            out var code,
            out var errors);

        try
        {
            if (result < 0 || code is null)
            {
                var details = errors is null
                    ? $"D3DCompile failed with HRESULT 0x{result:X8}."
                    : Marshal.PtrToStringAnsi(errors.GetBufferPointer(), checked((int)errors.GetBufferSize()));
                throw new InvalidOperationException(details);
            }

            var bytecode = new byte[checked((int)code.GetBufferSize())];
            Marshal.Copy(code.GetBufferPointer(), bytecode, 0, bytecode.Length);
            return bytecode;
        }
        finally
        {
            if (errors is not null) Marshal.FinalReleaseComObject(errors);
            if (code is not null) Marshal.FinalReleaseComObject(code);
        }
    }

    [ComImport]
    [Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3DBlob
    {
        [PreserveSig]
        nint GetBufferPointer();

        [PreserveSig]
        nuint GetBufferSize();
    }

    #pragma warning disable CA2101 // D3DCompile defines source names, entry points, and targets as LPCSTR.
    #pragma warning disable SYSLIB1054
    [DllImport("d3dcompiler_47.dll", ExactSpelling = true, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int D3DCompile(
        [In] byte[] sourceData,
        nuint sourceDataSize,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? sourceName,
        nint defines,
        nint include,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string entryPoint,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string target,
        uint flags1,
        uint flags2,
        out ID3DBlob? code,
        out ID3DBlob? errors);
    #pragma warning restore SYSLIB1054
    #pragma warning restore CA2101
}
