# IssueFlow – Bug & Project Management System

ASP.NET Core 8 MVC application with Identity, Jira-style workflows, bug tracking, subscriptions, and payments.

## Features

- Authentication (login / register / logout) with account lockout and rate limiting
- Roles: **Admin**, **ProjectManager**, **Developer**, **QATester**
- Project membership (global Identity role ≠ per-project role)
- Project CRUD and team members
- Issue lifecycle (canonical workflow):
  - `Backlog` → `ToDo` → `InProgress` → `CodeReview` → `ReadyForTest` → `Testing` → `Done`
  - Fail path: `Testing` → `Reopened` → `InProgress`
- Kanban board per project
- Bug lifecycle: `Reported` → `Assigned` → `Fixed` → `ReadyForReview` → `Verified` / `Reopened`
- Assignment requests (developers and QA request work; PM approves or offers)
- Deadline extensions and automatic performance rating
- In-app notifications and activity timeline
- Subscription plans (Free / Weekly / Monthly / Yearly) with usage limits
- SSLCommerz payment integration
- REST API + Swagger
- Serilog (console + rolling file logs)

## Architecture

```
Controllers (HTTP, auth, ViewBag only)
    ↓
Services (business rules)
    ↓
Unit of Work + Repositories
    ↓
ApplicationDbContext → SQL Server
```

- **Issue** and **Project** controllers do not use `DbContext` directly; they call `IIssueService` and `IProjectService`.
- Status and priority values are defined in `Constants/WorkflowStatuses.cs`.
- All timestamps are stored in **UTC** (`DateTime.UtcNow`).
- Data annotations provide validation on models, DTOs, and view models.

See [ARCHITECTURE.md](ARCHITECTURE.md) for layer details and permission notes.

## Canonical status constants

Defined in `Constants/WorkflowStatuses.cs`:

| Type | Constants |
|------|-----------|
| Issues | `IssueStatuses` |
| Bugs | `BugStatuses` |
| Requests | `AssignmentRequestStatuses` |
| Priority | `Priorities` |
| Issue type | `IssueTypes` |

Use these constants everywhere. Do not hard-code legacy values such as `"Open"` or `"Completed"`.  
Legacy values are mapped via `IssueStatuses.Normalize()`.

## Demo accounts (seeded)

| Role            | Email               | Password  |
|-----------------|---------------------|-----------|
| Admin           | admin@issueflow.com | Admin*123 |
| Project Manager | pm@issueflow.com    | ------- |
| Developer       | dev@issueflow.com   | ------- ` |
| QA Tester       | qa@issueflow.com    | --------  |

## How to run

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Open the HTTPS URL from `Properties/launchSettings.json` (for example `https://localhost:7xxx`).

If EF tools are missing:

```bash
dotnet tool install --global dotnet-ef
```

## Connection string

Default (LocalDB / SQL Express) is in `appsettings.json`.

For production, prefer User Secrets, environment variables, or a secret store (for example Azure Key Vault). Do not commit real payment credentials.

## Role permissions (summary)

| Role | Capabilities |
|------|----------------|
| **Admin** | Full access, user management, bypasses subscription limits |
| **Project Manager** | Projects, issues, assignments, status, team members |
| **Developer** | Assigned work, status progress, comments, extension requests |
| **QA Tester** | Report bugs, review fixes, mark work Done after testing |

## Optional: normalize legacy issue statuses

If an existing database still has old status strings:

```sql
UPDATE Issues SET Status = 'Backlog' WHERE Status IN ('Open', '');
UPDATE Issues SET Status = 'InProgress' WHERE Status = 'In Progress';
UPDATE Issues SET Status = 'ReadyForTest' WHERE Status = 'Resolved';
UPDATE Issues SET Status = 'Done' WHERE Status IN ('ReadyToLaunch', 'Closed', 'Completed');
```

## Project layout

```
IssueFlow/
├── Constants/          WorkflowStatuses, Priorities, IssueTypes
├── Controllers/        MVC + API
├── DTOs/               API and form input shapes
├── Data/               DbContext, seeder
├── Models/             Entities + ViewModels
├── Repositories/       Generic repository + Unit of Work
├── Services/           Business logic
├── Views/              Razor views
├── Middleware/         Exception handling
├── Filters/            Subscription checks
└── wwwroot/            Static files, uploads
```

## License / course use

Suitable for academic demonstration and as a base for further industrial hardening (tests, soft-delete, CI/CD).
