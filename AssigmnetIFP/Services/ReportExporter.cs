using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class ReportExporter
{
    /// <summary>
    /// Safely writes report text content to a file using asynchronous stream disposal (await using).
    /// Releases the file handle and stream on both success and exception paths.
    /// </summary>
    public static async Task<Result<string, DomainError>> ExportReportAsync(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result<string, DomainError>.Fail(
                new DomainError("InvalidFilePath", "The target file path cannot be empty."));
        }

        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Explicit FileStream and StreamWriter creation with await using for safe asynchronous disposal
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await using var writer = new StreamWriter(fileStream, Encoding.UTF8);
            await writer.WriteAsync(content);

            return Result<string, DomainError>.Ok(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<string, DomainError>.Fail(
                new DomainError("FileAccessDenied", $"Access to write file '{filePath}' was denied: {ex.Message}"));
        }
        catch (IOException ex)
        {
            return Result<string, DomainError>.Fail(
                new DomainError("FileWriteFailed", $"I/O failure while writing report '{filePath}': {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result<string, DomainError>.Fail(
                new DomainError("UnexpectedExportError", $"Unexpected error exporting report: {ex.Message}"));
        }
    }

    /// <summary>
    /// Yields lines from a file while keeping the resource bounded.
    /// The using blocks guarantee deterministic disposal of both reader and stream upon normal completion,
    /// exception, or early termination (e.g. Linq Take).
    /// </summary>
    public static IEnumerable<string> StreamLines(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(fileStream, Encoding.UTF8);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }

    public static string FormatAuthorReport(IReadOnlyList<AuthorReportItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AUTHOR PUBLISHING & EDITION AGGREGATION REPORT ===");
        sb.AppendLine($"Generated on {DateTime.UtcNow:u} | Total Authors: {items.Count}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-30} | {1,6} | {2,8} | {3,12}", "Author", "Books", "Editions", "Active Span"));
        sb.AppendLine("--------------------------------------------------------------------------------");

        if (items.Count == 0)
        {
            sb.AppendLine("(no records)");
        }
        else
        {
            foreach (var item in items)
            {
                string span = item.EarliestYear == item.LatestYear
                    ? $"{item.EarliestYear}"
                    : $"{item.EarliestYear} - {item.LatestYear}";
                sb.AppendLine(string.Format("{0,-30} | {1,6} | {2,8} | {3,12}", item.Author, item.BookCount, item.TotalEditions, span));
            }
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        return sb.ToString();
    }

    public static string FormatPriorityReport(
        IReadOnlyList<BookPriorityReportItem> items,
        IReadOnlyList<DecadeSummary> decades)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CURRIED SCORING & NATIVE PRIORITY REPORT ===");
        sb.AppendLine($"Total Books: {items.Count}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,4} | {1,-35} | {2,4} | {3,10} | {4,10}", "Rank", "Title", "Year", "Curried", "Native"));
        sb.AppendLine("--------------------------------------------------------------------------------");

        if (items.Count == 0)
        {
            sb.AppendLine("(no records)");
        }
        else
        {
            foreach (var item in items)
            {
                string shortTitle = item.Title.Length > 35 ? item.Title[..32] + "..." : item.Title;
                sb.AppendLine(string.Format("{0,4} | {1,-35} | {2,4} | {3,10:F2} | {4,10}",
                    item.Rank, shortTitle, item.PublishYear, item.CurriedScore, item.NativePriority));
            }
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("DECADE SUMMARIES (AGGREGATES):");
        sb.AppendLine(string.Format("{0,6} | {1,6} | {2,16}", "Decade", "Count", "Avg Native Score"));
        sb.AppendLine("--------------------------------------------------------------------------------");
        foreach (var d in decades)
        {
            sb.AppendLine(string.Format("{0,6}s | {1,6} | {2,16:F1}", d.Decade, d.BookCount, d.AverageNativePriority));
        }
        sb.AppendLine("--------------------------------------------------------------------------------");
        return sb.ToString();
    }
}
