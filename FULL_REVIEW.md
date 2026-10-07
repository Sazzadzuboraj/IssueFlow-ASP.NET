# IssueFlow – Full Project Review (English)

## What was broken and fixed

### Critical runtime bugs
1. **EF Core: `Priorities.SortKey()` in OrderBy** – not SQL-translatable → crash on `/Issue`.  
   **Fix:** Ternary OrderBy (`Critical`/`High`/`Medium`) in `IssueService`, `BugService`, `HomeController`.

2. **CS0236: `ReportBugViewModel.Priorities`** – property name clashed with `Constants.Priorities`.  
   **Fix:** Renamed to `PriorityOptions`; updated `Views/Bug/Report.cshtml`.

3. **Issue Details UI used legacy statuses** (`Open`, `In Progress`, `Resolved`).  
   **Fix:** Canonical workflow buttons + comments + PM status dropdown.

4. **Subscription gate** – users without a DB subscription row failed assign/request.  
   **Fix:** Free tier treated as active; seeder adds Yearly for demo accounts.

5. **Exception middleware** returned JSON for all errors (breaks browser MVC).  
   **Fix:** JSON only for `/api`; MVC rethrows to developer exception page.

6. **Cookie `SecurePolicy.Always`** – cookies dropped on HTTP.  
   **Fix:** `CookieSecurePolicy.SameAsRequest`.

7. **AssignmentRequest review queue** filtered `Resolved`.  
   **Fix:** `ReadyForTest` / `CodeReview` / `Testing`.

8. **Architecture (Issue/Project)** – controllers used DbContext.  
   **Fix:** `IIssueService` / `IProjectService` only; Create/Edit use DTOs.

### Data / seed
- Demo users + Yearly subscription + sample project, issues, assignment, bug.
- UTC timestamps; data annotations on models/DTOs/view models.

### Still using DbContext (acceptable for course scope)
- `HomeController`, `AssignmentRequestController`, `BoardController`, `BugController` (partial), `SubscriptionController`, `PaymentController`, `UserController`.

## How to run (Visual Studio)

```powershell
cd D:\cscProject\IssueFlow
dotnet restore
dotnet build
dotnet ef database update
dotnet run
```

Login: `admin@issueflow.com` / `Admin@123`

If statuses are wrong on old DB:

```sql
UPDATE Issues SET Status = 'Backlog' WHERE Status IN ('Open','');
UPDATE Issues SET Status = 'InProgress' WHERE Status = 'In Progress';
UPDATE Issues SET Status = 'ReadyForTest' WHERE Status = 'Resolved';
UPDATE Issues SET Status = 'Done' WHERE Status IN ('ReadyToLaunch','Closed','Completed');
```

## Module checklist

| Module | Expected |
|--------|----------|
| Login / roles | Demo accounts work |
| Dashboard | Role-specific counts |
| Projects | CRUD, members, soft-delete |
| Issues | List, filter, details, status, assign, comments |
| Board | Columns for canonical statuses |
| Bugs | Report, assign, submit fix, review |
| Assignment requests | Work + review queues |
| Subscriptions | Free limits; paid via payment |
| Notifications | Bell + list |
| API | `/swagger` in Development |

## Layer map

```
Controllers → Services → UnitOfWork/Repositories → EF Core → SQL Server
```

Constants: `Constants/WorkflowStatuses.cs`
