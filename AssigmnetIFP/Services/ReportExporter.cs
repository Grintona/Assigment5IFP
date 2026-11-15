using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class ReportExporter
{

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

    public static string FormatStudyPlan(StudyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var sb = new StringBuilder();
        sb.AppendLine("=== ACADEMIC STUDY PLAN ===");
        sb.AppendLine($"Topic: {plan.Topic} | Level: {plan.Difficulty} | Total Hours: {plan.TotalReadingHours}");
        sb.AppendLine($"Pacing: {plan.RecommendedPacing}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,6} | {1,-35} | {2,6} | {3}", "Week", "Book Resource", "Hours", "Objective"));
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var mod in plan.Modules)
        {
            string title = mod.Resource.Title.Length > 35 ? mod.Resource.Title[..32] + "..." : mod.Resource.Title;
            sb.AppendLine(string.Format("{0,6} | {1,-35} | {2,6} | {3}", mod.WeekNumber, title, mod.WeeklyReadingHours, mod.LearningObjective));
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        return sb.ToString();
    }

    public static async Task<Result<string, DomainError>> ExportStudyPlanAsync(string filePath, StudyPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return await ExportReportAsync(filePath, FormatStudyPlan(plan));
    }

    public static string FormatLoanAgreement(LoanState.Approved loan)
    {
        ArgumentNullException.ThrowIfNull(loan);

        var sb = new StringBuilder();
        sb.AppendLine("=== ACADEMIC LIBRARY LOAN CONFIRMATION ===");
        sb.AppendLine($"Loan ID: {loan.LoanId}");
        sb.AppendLine($"Borrower: Student {loan.StudentId} (Tier: {loan.Tier})");
        sb.AppendLine($"Due Date: {loan.DueDate:yyyy-MM-dd} | Late Fee Rate: ${loan.DailyLateFee:F2}/day");
        sb.AppendLine("Issued Resources:");
        for (int i = 0; i < loan.Books.Length; i++)
        {
            var b = loan.Books[i];
            sb.AppendLine($"  {i + 1}. {b.Title} (Key: {b.Key}, Editions: {b.EditionCount})");
        }
        sb.AppendLine("Status: APPROVED (Immutable loan agreement)");
        sb.AppendLine("--------------------------------------------------------------------------------");
        return sb.ToString();
    }

    public static async Task<Result<string, DomainError>> ExportLoanAgreementAsync(string filePath, LoanState.Approved loan)
    {
        ArgumentNullException.ThrowIfNull(loan);
        return await ExportReportAsync(filePath, FormatLoanAgreement(loan));
    }
}
