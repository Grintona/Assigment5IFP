using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public static class LoanWorkflow
{

    public static readonly Func<int, double> StandardFeePolicy =
        daysOverdue => daysOverdue > 0 ? daysOverdue * 1.50 : 0.0;

    public static readonly Func<int, double> AcademicDiscountFeePolicy =
        daysOverdue => daysOverdue > 0 ? daysOverdue * 0.50 : 0.0;

    public static Result<LoanState.Draft, DomainError> CreateDraft(
        string studentId,
        BorrowerTier tier,
        IEnumerable<string> requestedBookKeys,
        int requestedWeeks)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return Result<LoanState.Draft, DomainError>.Fail(
                new DomainError("InvalidStudentId", "Student ID must not be empty."));

        if (requestedWeeks <= 0)
            return Result<LoanState.Draft, DomainError>.Fail(
                new DomainError("InvalidLoanDuration", "Requested loan weeks must be strictly greater than zero."));

        var keys = requestedBookKeys?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
        if (keys.Length == 0)
            return Result<LoanState.Draft, DomainError>.Fail(
                new DomainError("MissingRequestedBooks", "At least one book key must be specified."));

        return Result<LoanState.Draft, DomainError>.Ok(
            new LoanState.Draft(studentId.Trim(), tier, keys, requestedWeeks));
    }

    public static Result<LoanState.Validated, DomainError> ValidateDraft(
        LoanState.Draft draft,
        IEnumerable<Book> catalog,
        Func<int, double>? lateFeePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);

        var (maxBooks, maxWeeks) = draft.Tier switch
        {
            BorrowerTier.Standard => (2, 4),
            BorrowerTier.Honors => (4, 8),
            BorrowerTier.Researcher => (8, 16),
            _ => (1, 2)
        };

        if (draft.RequestedBookKeys.Length > maxBooks)
        {
            return Result<LoanState.Validated, DomainError>.Fail(
                new DomainError(
                    "BorrowQuotaExceeded",
                    $"Tier '{draft.Tier}' is limited to {maxBooks} books (requested {draft.RequestedBookKeys.Length})."));
        }

        if (draft.RequestedWeeks > maxWeeks)
        {
            return Result<LoanState.Validated, DomainError>.Fail(
                new DomainError(
                    "LoanDurationExceeded",
                    $"Tier '{draft.Tier}' maximum loan duration is {maxWeeks} weeks (requested {draft.RequestedWeeks})."));
        }

        var resolvedBooks = ImmutableArray.CreateBuilder<Book>();
        foreach (var key in draft.RequestedBookKeys)
        {
            var bookOption = BookPipeline.FindBookOption(catalog, key);
            if (bookOption is Option<Book>.Some some)
            {
                resolvedBooks.Add(some.Value);
            }
            else
            {
                return Result<LoanState.Validated, DomainError>.Fail(
                    new DomainError("BookNotFound", $"Requested book with key '{key}' was not found in catalog."));
            }
        }

        var policy = lateFeePolicy ?? StandardFeePolicy;

        return Result<LoanState.Validated, DomainError>.Ok(
            new LoanState.Validated(
                draft.StudentId,
                draft.Tier,
                resolvedBooks.ToImmutable(),
                draft.RequestedWeeks,
                policy));
    }

    public static Result<LoanState.Approved, DomainError> ApproveLoan(
        LoanState state,
        DateTime? approvalTimestamp = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state switch
        {
            LoanState.Validated validated =>
                Result<LoanState.Approved, DomainError>.Ok(
                    new LoanState.Approved(
                        LoanId: $"LOAN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                        StudentId: validated.StudentId,
                        Tier: validated.Tier,
                        Books: validated.Books,
                        DueDate: (approvalTimestamp ?? DateTime.UtcNow).AddDays(validated.ApprovedWeeks * 7),
                        DailyLateFee: validated.LateFeePolicy(1)
                    )),

            LoanState.Draft =>
                Result<LoanState.Approved, DomainError>.Fail(
                    new DomainError("InvalidStateTransition", "Cannot approve a Draft loan without validation.")),

            LoanState.Approved =>
                Result<LoanState.Approved, DomainError>.Fail(
                    new DomainError("InvalidStateTransition", "This loan has already been approved.")),

            LoanState.Rejected rejected =>
                Result<LoanState.Approved, DomainError>.Fail(
                    new DomainError("InvalidStateTransition", $"Cannot approve a rejected loan: {rejected.Reason}")),

            _ =>
                Result<LoanState.Approved, DomainError>.Fail(
                    new DomainError("InvalidStateTransition", "Unknown loan state."))
        };
    }
}
