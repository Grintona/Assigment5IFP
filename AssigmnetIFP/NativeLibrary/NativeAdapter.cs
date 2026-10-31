using System;
using System.IO;
using System.Runtime.InteropServices;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Native;

public static class NativeAdapter
{
    private const int MaxRealisticBookAge = 1000;
    private const int MaxRealisticEditions = 50000;


    public static Result<ulong, string> CalculatePrioritySafe(int editionCount, int ratingsCount, int bookAge)
    {

        if (editionCount < 0 || editionCount > MaxRealisticEditions)
        {
            return Result<ulong, string>.Fail(
                $"Invalid edition count: {editionCount}. Allowed range: 0..{MaxRealisticEditions}."
            );
        }

        if (ratingsCount < 0)
        {
            return Result<ulong, string>.Fail(
                $"The ratings count cannot be negative: {ratingsCount}."
            );
        }

        if (bookAge < 0 || bookAge > MaxRealisticBookAge)
        {
            return Result<ulong, string>.Fail(
                $"Invalid book age: {bookAge}. Allowed range: 0..{MaxRealisticBookAge}."
            );
        }

        try
        {

            ulong score = NativeBridge.CalculateBookPriority(
                (ulong)editionCount,
                (ulong)ratingsCount,
                (ulong)bookAge
            );

            return Result<ulong, string>.Ok(score);
        }
        catch (DllNotFoundException ex)
        {
            return Result<ulong, string>.Fail($"Native library not found: {ex.Message}");
        }
        catch (BadImageFormatException ex)
        {
            return Result<ulong, string>.Fail(
                $"Incompatible native library format (x86-64 required): {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            return Result<ulong, string>.Fail($"System error while executing native code: {ex.Message}");
        }
    }
}