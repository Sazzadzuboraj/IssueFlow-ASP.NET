# Changelog

## Project file system + issue source uploads (2026-08-25)

- **Project folder on create:** `wwwroot/uploads/projects/{Id}_{SanitizedName}/` with `issues/` subfolder; `Project.FolderPath` stored in DB
- **IssueAttachment** model + `IssueAttachments` table — files organized by type: Source, Document, Config, Test, Other
- Path layout: `uploads/projects/{FolderPath}/issues/{IssueId}/{FileType}/{guid}{ext}`
- `IProjectFileService` / `ProjectFileService` — upload, download, delete, ensure folder
- Issue **Details**: developer/PM upload form; QA/reviewer can list & download files when issue is in CodeReview / ReadyForTest / Testing
- **ZIP download:** `DownloadIssueZip` — all files or one FileType; zip entries keep `{FileType}/{FileName}` layout
- SQL script: `Scripts/AddIssueAttachments.sql` for databases not using EF migrate
- Allowed extensions: common source, config, docs, images (max 20 MB / file)

## Professional hardening (2026)

### Status and data consistency

- Added `Constants/WorkflowStatuses.cs` (`IssueStatuses`, `BugStatuses`, `Priorities`, `IssueTypes`, …)
- `IssueStatuses.Normalize()` maps legacy values (`Open`, `In Progress`, `Resolved`, …) to the canonical set
- Issue create uses **Backlog**; completion rating triggers on **Done**
- Assignment request filters use canonical statuses
- Dashboards and board columns use the same vocabulary
- All `DateTime.Now` usages replaced with **`DateTime.UtcNow`**

### Data annotations

- Required, StringLength, Range, Display, EmailAddress, Phone, Compare, DataType applied across models, DTOs, and view models
- Clear English validation error messages

### Architecture (Option 1)

- Introduced `IProjectService` / `ProjectService`
- Expanded `IIssueService` (filtered list, assign, `IsAssigned`, `GetById`)
- `IUnitOfWork` exposes `ProjectMembers`
- **IssueController** and **ProjectController** no longer use `ApplicationDbContext`
- Issue Create/Edit views bind to `CreateIssueDto` / `UpdateIssueDto`
- Registered `IProjectService` in DI (`Program.cs`)

### Documentation

- README, ARCHITECTURE, and this changelog written in English
