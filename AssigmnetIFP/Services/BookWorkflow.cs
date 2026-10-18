using System.Collections.Immutable;
using StudyResourceFinder.Models;

namespace StudyResourceFinder.Services;

public abstract record BookWorkflowState
{
    private BookWorkflowState() { }

    public sealed record Pending(ValidatedSearch Search) : BookWorkflowState;

    public sealed record Completed(IReadOnlyList<Recommendation> Recommendations) : BookWorkflowState;

    public sealed record Rejected(string Reason) : BookWorkflowState;
}

public static class BookWorkflow
{
    public static ValidationResult<BookWorkflowState> Transition(BookWorkflowState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state switch
        {
            BookWorkflowState.Pending pending => new ValidationResult<BookWorkflowState>.Valid(
                new BookWorkflowState.Completed(
                    BookPipeline.FindResources(pending.Search))),
            BookWorkflowState.Completed => Invalid("The search has already been completed."),
            BookWorkflowState.Rejected rejected => Invalid(
                $"The workflow was rejected: {rejected.Reason}"),
            _ => Invalid("Unknown workflow state.")
        };
    }

    private static ValidationResult<BookWorkflowState> Invalid(string reason) =>
        new ValidationResult<BookWorkflowState>.Invalid(ImmutableArray.Create(reason));
}