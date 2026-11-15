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

    public sealed record Draft : LoanState
    {
        public string StudentId { get; }
        public BorrowerTier Tier { get; }
        public ImmutableArray<string> RequestedBookKeys { get; }
        public int RequestedWeeks { get; }

        internal Draft(
            string StudentId,
            BorrowerTier Tier,
            ImmutableArray<string> RequestedBookKeys,
            int RequestedWeeks)
        {
            this.StudentId = StudentId;
            this.Tier = Tier;
            this.RequestedBookKeys = RequestedBookKeys;
            this.RequestedWeeks = RequestedWeeks;
        }

        public void Deconstruct(out string studentId, out BorrowerTier tier, out ImmutableArray<string> requestedBookKeys, out int requestedWeeks)
        {
            studentId = StudentId;
            tier = Tier;
            requestedBookKeys = RequestedBookKeys;
            requestedWeeks = RequestedWeeks;
        }
    }

    public sealed record Validated : LoanState
    {
        public string StudentId { get; }
        public BorrowerTier Tier { get; }
        public ImmutableArray<Book> Books { get; }
        public int ApprovedWeeks { get; }
        public Func<int, double> LateFeePolicy { get; }

        internal Validated(
            string StudentId,
            BorrowerTier Tier,
            ImmutableArray<Book> Books,
            int ApprovedWeeks,
            Func<int, double> LateFeePolicy)
        {
            this.StudentId = StudentId;
            this.Tier = Tier;
            this.Books = Books;
            this.ApprovedWeeks = ApprovedWeeks;
            this.LateFeePolicy = LateFeePolicy;
        }

        public void Deconstruct(out string studentId, out BorrowerTier tier, out ImmutableArray<Book> books, out int approvedWeeks, out Func<int, double> lateFeePolicy)
        {
            studentId = StudentId;
            tier = Tier;
            books = Books;
            approvedWeeks = ApprovedWeeks;
            lateFeePolicy = LateFeePolicy;
        }
    }

    public sealed record Approved : LoanState
    {
        public string LoanId { get; }
        public string StudentId { get; }
        public BorrowerTier Tier { get; }
        public ImmutableArray<Book> Books { get; }
        public DateTime DueDate { get; }
        public double DailyLateFee { get; }

        internal Approved(
            string LoanId,
            string StudentId,
            BorrowerTier Tier,
            ImmutableArray<Book> Books,
            DateTime DueDate,
            double DailyLateFee)
        {
            this.LoanId = LoanId;
            this.StudentId = StudentId;
            this.Tier = Tier;
            this.Books = Books;
            this.DueDate = DueDate;
            this.DailyLateFee = DailyLateFee;
        }

        public void Deconstruct(out string loanId, out string studentId, out BorrowerTier tier, out ImmutableArray<Book> books, out DateTime dueDate, out double dailyLateFee)
        {
            loanId = LoanId;
            studentId = StudentId;
            tier = Tier;
            books = Books;
            dueDate = DueDate;
            dailyLateFee = DailyLateFee;
        }
    }

    public sealed record Rejected : LoanState
    {
        public string StudentId { get; }
        public string Reason { get; }

        internal Rejected(string StudentId, string Reason)
        {
            this.StudentId = StudentId;
            this.Reason = Reason;
        }

        public void Deconstruct(out string studentId, out string reason)
        {
            studentId = StudentId;
            reason = Reason;
        }
    }
}
