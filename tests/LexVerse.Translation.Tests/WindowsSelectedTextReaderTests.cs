using System.Reflection;
using System.Runtime.InteropServices;
using LexVerse.Infrastructure.Windows;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class WindowsSelectedTextReaderTests
{
    [Fact]
    public void NativeInput_MatchesWindowsInputAbi()
    {
        var input = typeof(WindowsSelectedTextReader).GetNestedType("NativeInput", BindingFlags.NonPublic)!;

        // Windows requires sizeof(INPUT): 40 on x64, 28 on x86.
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf(input));
        Assert.Equal(IntPtr.Size == 8 ? 8 : 4, Marshal.OffsetOf(input, "Data").ToInt32());
    }
}
