    using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace StudyResourceFinder.Native; 

internal static class NativeBridge
{
    public const string LibraryName = "calc";

    static NativeBridge()
    {

        NativeLibrary.SetDllImportResolver(typeof(NativeBridge).Assembly, DllImportResolver);
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        string baseDir = AppContext.BaseDirectory;
        string fileName;

        if (OperatingSystem.IsLinux())
        {
            fileName = "libcalc.so";
        }
        else if (OperatingSystem.IsWindows())
        {
            fileName = "libcalc.dll";
        }
        else
        {
            return IntPtr.Zero;
        }

        string fullPath = Path.Combine(baseDir, fileName);

        if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out IntPtr handle))
        {
            return handle;
        }

        return IntPtr.Zero;
    }


    [DllImport(LibraryName, EntryPoint = "calculate_book_priority")]
    internal static extern ulong CalculateBookPriority(ulong editionCount, ulong ratingsCount, ulong bookAge);
}