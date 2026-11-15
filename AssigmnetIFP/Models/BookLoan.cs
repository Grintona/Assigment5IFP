using System;
using System.Collections.Immutable;

namespace StudyResourceFinder.Models;

public enum BorrowerTier
{
    Standard,
    Honors,
    Researcher
}

public abstract record LoanState
{
    private LoanState() { }

    public sealed record Draft(
        string StudentId,
        BorrowerTier Tier,
        ImmutableArray<string> RequestedBookKeys,
        int RequestedWeeks) : LoanState;

    public sealed record Validated(
        string StudentId,
        BorrowerTier Tier,
        ImmutableArray<Book> Books,
        int ApprovedWeeks,
        Func<int, double> LateFeePolicy) : LoanState;

    public sealed record Approved(
        string LoanId,
        string StudentId,
        BorrowerTier Tier,
        ImmutableArray<Book> Books,
        DateTime DueDate,
        double DailyLateFee) : LoanState;

    public sealed record Rejected(
        string StudentId,
        string Reason) : LoanState;
}
