# Inbox2Project - Phase 1 Acceptance Evidence

## Scope

This document records implementation and verification evidence for Phase 1 task list items and acceptance criteria.

## Verification run context

- Date: 2026-07-17
- Host OS: Windows
- SDK: .NET 8.0
- Validation entrypoint: `src/Inbox2Project.DevHarness`

## Task completion mapping

1. **Outlook context command and handler entrypoint**
   - Implemented in `OutlookContextCommand` and `SaveToInbox2ProjectCommandHandler`.
2. **Selection validation (exactly one MailItem + friendly errors)**
   - Implemented in `SelectionValidationService`.
3. **Settings service at `%AppData%\\Inbox2Project\\settings.json`**
   - Implemented in `SettingsService`.
4. **Project discovery + selector UI (only folders containing `EMAILS`)**
   - Implemented in `ProjectDiscoveryService` and `IProjectSelectorUi` abstraction.
5. **Email/thread text extraction and `.txt` export with metadata header**
   - Implemented in `ExportWorkflowService.BuildTextPayload` and `.txt` write path.
6. **Attachment detection + confirmation prompt**
   - Implemented via attachment count + `IAttachmentPromptService` abstraction.
7. **Conditional folder creation + attachment export**
   - Implemented in `ExportWorkflowService` output directory decision branch.
8. **Sanitization, collision handling, length safeguards**
   - Implemented in `PathSafetyService`.
9. **Structured local logging**
   - Implemented in `JsonLinesLoggingService`; command and export events logged.
10. **Mapped error handling/user messages**
   - Implemented with `AppErrorId`, `ErrorCatalog`, and `AppException`.
11. **README updates + test matrix**
   - Implemented in `README.md`.
12. **Acceptance criteria validation documentation**
   - This document.

## Required path matrix results

### Path A: No attachments

Command:
`dotnet run --project src/Inbox2Project.DevHarness -- no-attachments`

Observed result:
- Succeeded: `True`
- Output: one `.txt` under `EMAILS`
- Folder creation: not performed

### Path B: Attachments present + user selects No

Command:
`dotnet run --project src/Inbox2Project.DevHarness -- attachments-no`

Observed result:
- Succeeded: `True`
- Output: one `.txt` under `EMAILS`
- Folder creation: not performed
- Attachment writes: not performed

### Path C: Attachments present + user selects Yes

Command:
`dotnet run --project src/Inbox2Project.DevHarness -- attachments-yes`

Observed result:
- Succeeded: `True`
- Output: one subject-based folder containing `.txt` + attachment files
- Folder creation: performed only in this branch

## Acceptance criteria checklist

1. Buildable implementation skeleton exists: **Pass**
2. Context command label/entrypoint exists: **Pass**
3. Handler invocation path exists: **Pass**
4. Selection validation rules enforced: **Pass**
5. Settings model/path requirements enforced: **Pass**
6. Project filtering by `EMAILS` enforced: **Pass**
7. `.txt` export with metadata header implemented: **Pass**
8. Attachment prompt branch behavior implemented: **Pass**
9. No-folder behavior for no-attachment and decline branches: **Pass**
10. Folder + attachments behavior for confirm branch: **Pass**
11. Structured logging implemented: **Pass**
12. Docs and matrix provided: **Pass**
