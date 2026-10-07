# IssueFlow Architecture

## Overview

IssueFlow is an ASP.NET Core 8 MVC application that follows a layered design:

1. **Presentation** – Controllers and Razor views  
2. **Application / domain services** – Business rules  
3. **Data access** – Unit of Work and generic repositories  
4. **Persistence** – EF Core `ApplicationDbContext` and SQL Server  

Identity handles authentication and global roles. Project membership is stored separately (`ProjectMember`) so a user’s role can differ per project.

## Layer responsibilities

### Controllers

- Accept HTTP requests, enforce `[Authorize]` / roles  
- Map form or API input to DTOs / view models  
- Call services  
- Set `TempData`, `ViewBag`, and choose views or redirects  

**IssueController** and **ProjectController** do not inject `ApplicationDbContext`. They depend only on services and `UserManager`.

Other controllers (for example Home, Board, AssignmentRequest) may still use the context in places; migrating them to the same pattern is recommended.

### Services

| Service | Responsibility |
|---------|----------------|
| `IIssueService` | Issue CRUD, filters, assign, status, comments, extensions, rating triggers |
| `IProjectService` | Project CRUD, soft-delete (`IsActive`), members |
| `IBugService` | Bug report, assign, fix, review |
| `IProjectPermissionService` | Membership and allowed status transitions |
| `ISubscriptionService` | Plan limits |
| `INotificationService` | In-app notifications |
| `IActivityLogService` | Audit timeline |
| `IRatingService` | Automatic scores on completion |
| `ISslCommerzService` | Payment gateway |

### Repositories

- `IGenericRepository<T>` – basic CRUD and queries  
- `IUnitOfWork` – shared context and `SaveChangesAsync`  
- Entities exposed: Issues, Projects, Assignments, Comments, Bugs, Ratings, Notifications, **ProjectMembers**, etc.

### Models vs DTOs vs ViewModels

| Type | Use |
|------|-----|
| **Entity** (`Models/`) | Database shape, data annotations for persistence and some forms |
| **DTO** (`DTOs/`) | API and issue create/update input; no navigation cycles |
| **ViewModel** | Complex screens (login, assign, report bug, dashboards) |

Issue **Create** / **Edit** bind to `CreateIssueDto` and `UpdateIssueDto`, not the entity directly.

## Issue workflow

```
Backlog → ToDo → InProgress → CodeReview → ReadyForTest → Testing → Done
                                              ↑              |
                                              └── Reopened ←─┘
```

Transitions are constrained by `ProjectPermissionService` (project role + assignee). Admins and project managers may force transitions where the UI allows.

Constants: `IssueFlow.Constants.IssueStatuses`.

## Bug workflow

```
Reported → Assigned → Fixed → ReadyForReview → Verified
                         ↑           |
                         └── Reopened ┘
```

Constants: `IssueFlow.Constants.BugStatuses`.

## Security notes

- Identity password rules and lockout configured in `Program.cs`  
- Rate limiting on auth and API policies  
- Cookie: HttpOnly, Secure, sliding expiration  
- Basic security headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy)  
- Payment store credentials should not stay in committed `appsettings.json` for production  

## Time and data conventions

- Store all `DateTime` values in **UTC**  
- Prefer constants for status and priority strings  
- Prefer service methods over controller-level EF queries for new code  

## Project file system (source submissions)

When a **project is created**, `ProjectService` calls `IProjectFileService.EnsureProjectFolderAsync`:

```
wwwroot/uploads/projects/{ProjectId}_{SanitizedName}/
    issues/
```

`Project.FolderPath` stores the folder name (e.g. `12_MyApp`).

Developers upload **issue source files** from **Issue Details**. Files are stored as:

```
uploads/projects/{FolderPath}/issues/{IssueId}/{FileType}/{guid}{ext}
```

`FileType` folders: `Source`, `Document`, `Config`, `Test`, `Other`.

Metadata is in `IssueAttachments` (linked to `Issue`). Reviewers (QA / PM) see and download the same files on Details when the issue is in **CodeReview**, **ReadyForTest**, or **Testing**.

**ZIP download:** `GET /Issue/DownloadIssueZip?issueId={id}&fileType={optional}` builds an in-memory ZIP via `BuildIssueZipAsync`. Entries preserve `{FileType}/{originalFileName}` (duplicate names get `_2`, `_3`, …). UI: “Download all (ZIP)” plus per-type “ZIP Source / Document / …”.

Service: `IProjectFileService` / `ProjectFileService`.  
SQL for existing DBs: `Scripts/AddIssueAttachments.sql`.

## Extending the design

Suggested next steps for a more industrial setup:

1. Move remaining controllers off `DbContext`  
2. Soft-delete for issues (query filters)  
3. Optimistic concurrency (`RowVersion`)  
4. Unit and integration tests  
5. Health checks and Docker  
