# Academic Study Resource & Lending Management System
**Final Capstone Project | Introduction to Functional Programming (Academic Year 2026–2027)**

---

## 1. Project Overview & Team Identification

| Property | Details |
|---|---|
| **Author / Student** | **Gulyayev Mikhail** |
| **Academic Group** | **IT-2512** |
| **Submission Category** | **Individual Project (Team of 1)** |
| **Implemented Scope** | 100% of functional core, domain models, 3 workflows, LINQ analytics, safe persistence, native assembly, and 62 automated tests |
| **Target Platform** | **.NET 10.0** (`net10.0`), C# 13, x86-64 NASM AMD64 Assembly |
| **Official Reports** | Available in `docs/`: [`Group_Project_Report.pdf`](docs/Group_Project_Report.pdf) and [`Individual_Contribution_Report.pdf`](docs/Individual_Contribution_Report.pdf) |

### Problem Statement
In academic institutions, students and researchers frequently face information overload when searching for curriculum resources across vast catalogs. Concurrently, academic libraries often struggle with inconsistent lending rules, temporal bugs (such as loans being approved prior to checking borrower quotas), and unhandled runtime exceptions during data ingest.

The **Academic Study Resource & Lending Management System** solves these challenges by combining:
1. **Mathematical Purity:** A pure domain core isolated from side-effecting I/O and Console calls.
2. **Deterministic Processing:** Immutable collections and deterministic tie-breaker sorting for reproducible outputs.
3. **Temporal Safety:** A closed algebraic state machine preventing invalid operation sequencing.
4. **Zero-Null Architecture:** Monadic `Option<T>` and `Result<T, DomainError>` types with monadic `Map` and `Bind` composition.
5. **Hardware Integration:** Low-level x86-64 assembly priority scoring integrated seamlessly via P/Invoke.

---

## 2. Coherent Domain Models

The application models the academic study and library ecosystem through three interconnected, immutable domain models:

```
+---------------------------------------------------------------------------------+
|                                    DOMAIN MODELS                                |
|                                                                                 |
|   +-------------------+       +-----------------------+     +-----------------+ |
|   |       Book        | <==== |      StudyModule      |     |    LoanState    | |
|   |-------------------|       |-----------------------|     |-----------------| |
|   | Title: string     |       | WeekNumber: int       |     | Draft           | |
|   | Authors: ImmArray |       | Resource: Book        |     | Validated       | |
|   | PublishYear: int? |       | WeeklyHours: int      |     | Approved        | |
|   | EditionCount: int |       | Objective: string     |     | Rejected        | |
|   | Key: string       |       +-----------------------+     +-----------------+ |
|   | Languages: ImmArr |                   ^                                     |
|   +-------------------+                   |                                     |
|                               +-----------------------+                         |
|                               |       StudyPlan       |                         |
|                               |-----------------------|                         |
|                               | Topic: string         |                         |
|                               | Difficulty: enum      |                         |
|                               | Modules: ImmArray     |                         |
|                               | TotalHours: int       |                         |
|                               +-----------------------+                         |
+---------------------------------------------------------------------------------+
```

### 1. `Book` (`Models/Book.cs`)
The foundational domain resource representing academic literature:
- Title and OpenLibrary catalog key (mandatory non-empty strings).
- Publication year (optional integer; unknown years represented explicitly as `null`).
- Total edition count (non-negative integer; explicit `0` is preserved).
- Immutable collections (`ImmutableArray<string>`) for authors and languages. Defensively copied upon parsing to guarantee caller immutability.

### 2. `StudyPlan` & `StudyModule` (`Models/StudyPlan.cs`)
Structured curriculum representations generated for students:
- `StudyPlanDifficulty`: Closed enum (`Beginner`, `Intermediate`, `Advanced`).
- `StudyModule`: Positional record linking a specific `Book` resource to a study week, allocated reading hours, and tailored pedagogical objectives.
- `StudyPlan`: Immutable record holding the full course modules, computed total reading hours, and recommended pacing summary.

### 3. `LoanState` (`Models/BookLoan.cs`)
A closed algebraic record hierarchy enforcing the academic lending lifecycle:
- `LoanState.Draft`: Holds raw student ID, `BorrowerTier`, requested book keys, and duration.
- `LoanState.Validated`: Contains resolved catalog `ImmutableArray<Book>`, verified tier quotas, and higher-order fee policy delegate.
- `LoanState.Approved`: Confirmed loan carrying unique loan ID, due date, daily late fee rate, and approved items.
- `LoanState.Rejected`: Captures domain rejection reasons without mutating prior state.

---

## 3. Three Meaningful Workflows

The application implements three complete, distinct workflows that combine structured input, domain rules, and verifiable results:

### Workflow 1: Resource Discovery & Policy-Based Scoring
- **Purpose:** Curates, scores, and deterministically ranks study resources from OpenLibrary or local datasets.
- **Inputs:** Raw data source (API query or `Data/books.json`), minimum publication year threshold, and scoring policy.
- **Pipeline:** Composed of three pure stages:
  $$\text{ValidatedSearch} \xrightarrow{\text{Bind}} \text{FilterBooksResult} \xrightarrow{\text{Bind}} \text{ScoreBooksResult} \xrightarrow{\text{Map}} \text{RankBooks}$$
- **Business Rules:**
  1. *Selection:* Books must have a known year $\ge \text{minimumYear}$.
  2. *Higher-Order Scoring:* Applies a delegate policy (Popularity, Recency, or Curried weights).
  3. *Deterministic Tie-Breaking:* Primary sorting by score descending; secondary sorting by book title ascending (ordinal).
- **Result:** Immutable list of ranked `Recommendation` items.

### Workflow 2: Personalized Study Plan Generator
- **Purpose:** Automatically synthesizes a structured weekly study syllabus for a chosen topic and student level.
- **Inputs:** Curriculum topic, student difficulty (`Beginner`, `Intermediate`, `Advanced`), target weeks (1..52), and max books per author.
- **Pipeline Stages:**
  1. `FilterCurriculumResources`: Enforces difficulty-based publication thresholds (Beginner: 1990+, Intermediate: 2005+, Advanced: 2015+).
  2. `ConstrainCurriculum`: Higher-order function applying `EnforceAuthorDiversity` to prevent any single author from dominating the syllabus.
  3. `ScheduleModules`: Uses **pattern matching on difficulty** to calculate weekly study hours (Beginner: 4h, Intermediate: 8h, Advanced: 12h) and tailored learning objectives.
- **Result:** Validated `StudyPlan` with weekly milestones and workload analytics.

### Workflow 3: Safe Multi-Stage Book Loan & Reservation
- **Purpose:** Manages student book borrowing while strictly enforcing lending quotas and temporal safety.
- **Inputs:** Student ID, student tier (`Standard`, `Honors`, `Researcher`), book keys, requested weeks.
- **State Machine & Rules:**
  1. `CreateDraft`: Syntactic validation of student identity and positive duration.
  2. `ValidateDraft`: Uses `Option<Book>` to verify each book's presence in the catalog. Uses **pattern matching on `BorrowerTier`** to enforce book limits (Standard: max 2, Honors: max 4, Researcher: max 8) and duration limits.
  3. `ApproveLoan`: Rejects invalid sequencing (calling approval on unvalidated draft or re-approving an approved loan). Evaluates dynamic late fee policy delegate and issues an immutable `Approved` loan agreement.
- **Result:** Confirmed `LoanState.Approved` record with unique loan ID and calculated due date.

---

## 4. Technical Architecture & Functional Techniques

```
+-----------------------------------------------------------------------------------+
|                        BOUNDARY LAYER (Side Effects & I/O)                        |
|   OpenLibrary API  /  books.json  /  Console ReadLine  /  FileStream Export       |
+-----------------------------------------------------------------------------------+
                                      |
                           (Parse & Validate DTOs)
                                      v
+-----------------------------------------------------------------------------------+
|                            PURE FUNCTIONAL CORE                                   |
|                                                                                   |
|  * Option<T> Monad (Some / None)         * Result<T, DomainError> Monad           |
|  * Immutable Models & Collections        * Higher-Order Functions & Currying      |
|  * Pattern Matching Domain Decisions     * Safe State Transitions (No Coupling)   |
|  * LINQ Analytics & Tie-Breakers         * Deferred Execution (yield return)      |
+-----------------------------------------------------------------------------------+
                                      |
                     (Pure Results / Formatted Text)
                                      v
+-----------------------------------------------------------------------------------+
|                        BOUNDARY OUTPUT (Deterministic Side Effects)               |
|         Console Printing   /   await using File Exports (reports/*.txt)           |
+-----------------------------------------------------------------------------------+
```

### 4.1 Functional Error Handling: `Option<T>` and `Result<T, E>`
- **Monadic `Option<T>` (`Models/Option.cs`):** Completely eliminates `null` for optional lookups. Implements `Some`, `None`, `Map`, `Bind`, `Match`, and `ToResult`. Used in `BookPipeline.FindBookOption` to resolve catalog items cleanly.
- **Monadic `Result<T, E>` (`Models/Result.cs`):** Represents fallible business operations. `Map` transforms values on success; `Bind` chains fallible continuations. Both short-circuit immediately on failure without executing subsequent steps or mutating state.
- **Boundary Exception Handling (`OpenLibraryClient.cs`, `ReportExporter.cs`):** Catches operational I/O exceptions (`FileNotFoundException`, `JsonException`, `UnauthorizedAccessException`) at input/output boundaries and translates them into structured `DomainError` values.

### 4.2 Higher-Order Functions & Currying
- **Curried Scorer (`Services/CurriedScoring.cs`):**
  $$\text{CreateScore} : \text{ScoringWeights} \rightarrow (\text{Book} \rightarrow \text{double})$$
  Partial application produces pre-configured scorers without global state:
  ```csharp
  public static readonly Func<Book, double> PopularityBiasedScorer =
      CreateScore(new ScoringWeights(EditionWeight: 1.5, RecencyWeight: 0.2));

  public static readonly Func<Book, double> RecencyBiasedScorer =
      CreateScore(new ScoringWeights(EditionWeight: 0.1, RecencyWeight: 2.5));
  ```
- **Author Diversity Constraint:** `StudyPlanWorkflow.EnforceAuthorDiversity` accepts a higher-order selector delegate to balance author frequency across curriculum modules.
- **Configurable Fee Policies:** `LoanWorkflow` accepts dynamic delegates (`StandardFeePolicy`, `AcademicDiscountFeePolicy`) to compute overdue rates.

### 4.3 Safe Operation Sequence & Temporal Decoupling
Dependencies between operations are made explicit via types:
- A caller cannot construct a valid search request without the private constructor factory `ValidatedSearch.CreateResult`.
- A caller cannot approve a loan directly from a `Draft`: `LoanWorkflow.ApproveLoan` pattern-matches on `LoanState` and rejects unvalidated drafts with `InvalidStateTransition`.
- Repeated transitions on `Approved` or `Rejected` states are cleanly rejected without mutation.

### 4.4 Custom Extension Methods
Implemented in `Services/BookExtensions.cs`:
1. `book.GetAge(referenceYear)`: Derived domain property computed on `Book`.
2. `books.WithMinimumEditions(minEditions)`: Idiomatic query pipeline filter extending `IEnumerable<Book>`.

### 4.5 Analytical LINQ Reports
Implemented in `Services/LinqReports.cs`, completely separated from display:
- **Report 1 (Author Publishing & Edition Aggregation):** Combines `WithMinimumEditions`, `Where`, `SelectMany` (author flattening), `GroupBy(author)`, and aggregates `Count()`, `Sum()`, `Min()`, `Max()`, ordered by `TotalEditions` descending with an ordinal author tie-breaker.
- **Report 2 (Curried & Native Priority Dynamics):** Projects age, curried functional score, and native assembly score; groups by decade (`(year / 10) * 10`) to calculate average priority scores across eras.

### 4.6 Deferred Execution & Safe Resource Disposal
- **Lazy Streaming (`Services/DeferredExecution.cs`):** `StreamScoreBooks` employs `yield return` to defer evaluation until elements are enumerated.
- **Deliberate Materialization Boundaries:** Iterators are materialized via `.ToImmutableArray()` or `.ToList()` only when stable snapshots are required for sorting (`OrderBy`) or repeated display.
- **Safe Asynchronous Disposal (`Services/ReportExporter.cs`):** `ExportReportAsync` uses `await using` on `FileStream` and `StreamWriter` to guarantee file handle release on all code paths.
- **Iterator Lock Release:** `StreamLines` wraps file reading in `using` inside the iterator method, ensuring early termination (`Take(1)`) releases OS file locks immediately.

### 4.7 Hardware Acceleration: AMD64 Native Assembly
- Implemented in AMD64 assembly (`native/linux/library_linux.asm` and `native/windows/library_windows.asm`):
  $$\text{score} = (\text{editions} \times 10) + \lfloor\text{ratings} / 2\rfloor - \text{age}$$
- C# P/Invoke bridge (`NativeBridge.cs`) uses `NativeLibrary.SetDllImportResolver` to dynamically locate `libcalc.so` / `libcalc.dll` relative to `AppContext.BaseDirectory`.
- `NativeAdapter.cs` validates inputs (clamping negative numbers and checking max bounds) before calling unmanaged code.

---

## 5. Automated Testing Suite

The test project contains **62 automated tests** implemented in xUnit 2.9.3, providing 100% deterministic coverage:

| Category | Count | Scope & Verification | Status |
|---|---|---|---|
| **Pure Functions & Domain Validation** | 34 | Age calculation, edition filtering, author diversity constraints, difficulty pacing, curried partial application, Option Map/Bind/Match, search factory validation | **PASSED** |
| **Complete Workflows** | 18 | End-to-end execution of Workflow 1 (scoring/ranking), Workflow 2 (study plan scheduling), Workflow 3 (multi-stage loan), and live API integration | **PASSED** |
| **State Machine & Temporal Safety** | 5 | Rejection of unvalidated draft approvals, prevention of duplicate approvals, preservation of rejected state | **PASSED** |
| **Persistence & Resource Disposal** | 5 | Missing file handling, malformed JSON recovery, safe export writing, early iterator file lock release (`Take(1)`) | **PASSED** |

### Verified Test Outcome
```text
Тестовый запуск для Assignment1FP.Tests.dll (.NETCoreApp,Version=v10.0)
Версия VSTest 18.0.2-dev (x64)

Запуск выполнения тестов; подождите...
Общее количество тестовых файлов (1), соответствующих указанному шаблону.

Пройден!   : не пройдено     0, пройдено    62, пропущено     0, всего    62, длительность 93 ms.
```

### Defects Discovered and Resolved During Testing
1. **Premature Loan Approvals:** Automated tests caught an invalid sequence where drafts could be approved directly. Fixed by enforcing typed state transitions (`Draft -> Validated -> Approved`).
2. **VSTest Runner Dynamic Library Resolution:** Tests initially failed finding `libcalc.so` due to working directory discrepancies. Fixed by configuring dynamic DLL resolution via `AppContext.BaseDirectory`.
3. **Resource Leak on Early Iteration Termination:** Early `Take(1)` iteration previously kept file streams locked. Resolved by encapsulating stream initialization in `using` blocks within the `yield return` iterator.

---

## 6. Project Limitations & Known Shortcomings

1. **API Rate Limiting & Page Depth:** OpenLibrary search is configured with a deterministic limit of 10 items (`limit=10`) per query without pagination.
2. **Session Persistence:** Catalog updates and loans are held in memory during the session and exported to disk as structured report files (`reports/*.txt`). An external SQL database was deliberately omitted to preserve zero-dependency functional purity.
3. **Defensive Metadata Handling:** Missing author or language lists in raw JSON records are treated as empty arrays rather than rejecting otherwise valid books.
4. **Assembly Architecture:** The native routine is compiled for x86-64 AMD64 architectures. Non-x86 platforms (e.g. ARM64) require a managed fallback or recompilation.

---

## 7. Build, Run, and Test Instructions

### Prerequisites
- .NET 10.0 SDK (`dotnet --version`)
- GCC and NASM (for Linux native assembly build)

### Build and Run Commands
```bash
# 1. Compile the native shared library:
make -C native/linux

# 2. Run the full automated test suite (all 62 tests):
dotnet test Assignment5.slnx

# 3. Launch the interactive application:
dotnet run --project AssigmnetIFP
```

---

## 8. Oral Defense Guide: What to Demonstrate

When presenting and defending the project:

1. **Demonstrate All 3 Workflows (Option 8):**
   - Launch application (`dotnet run --project AssigmnetIFP`), select option `2` (local file `Data/books.json`), enter year `2000`, and choose **Option 8** (Full Capstone Demonstration).
   - Show console output for Workflow 1 (Recommendations), Workflow 2 (Study Plan with weekly modules), and Workflow 3 (Approved loan with student tier and due date).
2. **Demonstrate Interactive Workflows:**
   - Run **Option 2** (Workflow 2): Generate a study plan for "Algorithms", choose difficulty `Intermediate` (8 hrs/week) or `Advanced` (12 hrs/week), and show how weekly modules and objectives adapt.
   - Run **Option 3** (Workflow 3): Demonstrate tier quotas. Attempt to request 3 books under `Standard` tier (max 2 books allowed) and show the structured `BorrowQuotaExceeded` domain failure. Then request 2 books and show successful approval.
3. **Demonstrate LINQ Reports & Extension Methods:**
   - Run **Option 4** (Report 1): Show author grouping, count, edition sum, and deterministic tie-breaker sorting. Show calls to `WithMinimumEditions`.
   - Run **Option 5** (Report 2): Show age calculation (`GetAge`), curried score, real native assembly priority score, and decade summaries.
4. **Demonstrate Currying & Partial Application (Option 6):**
   - Show how `PopularityBiasedScorer` and `RecencyBiasedScorer` invert book rankings on the exact same dataset without any mutable state.
5. **Demonstrate Safe File Export (Option 7):**
   - Show generated files in `reports/` (`author_statistics_report.txt`, `priority_native_report.txt`, `sample_study_plan.txt`, `sample_loan_agreement.txt`).
   - Highlight asynchronous stream disposal using `await using`.
6. **Demonstrate Automated Test Suite:**
   - Execute `dotnet test Assignment5.slnx` in terminal to confirm all 62 tests pass deterministically.
