# Inbox2Project - Phase 0 Design Baseline (V1)

## Scope of this phase

This document delivers **Phase 0 only**:
- technical approach and constraints,
- operation sequence,
- error taxonomy with user-facing messages,
- finalized acceptance criteria for Phase 1.

No implementation for later phases is included.

## Assumptions and constraints (locked for V1)

1. **Outlook target:** Classic Outlook Desktop on Windows 10/11 (COM/VSTO-compatible model).
2. **Deployment:** Local machine add-in installation (per-user install supported first).
3. **Data location:** All data remains local; no cloud service dependency in V1.
4. **Configuration store:** JSON file under `%AppData%\\Inbox2Project\\settings.json`.
5. **Project structure rule:** Valid project folder must contain an `EMAILS` subfolder.
6. **Safety rule:** Original Outlook item is never modified or deleted.
7. **Naming rule baseline:** Subject-based naming with sanitization and collision suffixing.

## V1 architecture baseline

### Components

1. **Outlook Add-in Layer**
   - Adds context command: `Save to Inbox2Project`.
   - Validates selected item type.
   - Invokes orchestration workflow.

2. **Workflow Service**
   - Coordinates operation pipeline:
     - selection validation,
     - project selection,
     - EML save,
     - attachment export (when present),
     - result shaping for UI/logging.

3. **Configuration Service**
   - Loads/saves settings JSON.
   - Provides `ProjectsRoot`.
   - Stores `LastSelectedProject`.

4. **Project Discovery Service**
   - Scans one level below `ProjectsRoot`.
   - Includes only directories containing `EMAILS`.

5. **Filesystem Service**
   - Sanitizes names.
   - Resolves collisions with `_1`, `_2`, ... suffixing.
   - Creates files/folders.
   - Performs preflight write checks and path validation.

6. **Logging Service**
   - Writes local structured logs (success/failure and error details).

7. **UI Layer**
   - Project selection dialog with refresh.
   - Success/error notifications with actionable text.

## Sequence flow (V1, end-to-end)

1. User right-clicks selected Outlook item and chooses `Save to Inbox2Project`.
2. Add-in validates that exactly one supported mail item is selected.
3. Configuration service loads settings.
4. If `ProjectsRoot` is missing/invalid, UI prompts user to configure it.
5. Project discovery scans `ProjectsRoot` and lists valid projects (those with `EMAILS`).
6. User confirms project in selector (default to last selected when available).
7. Workflow resolves target `EMAILS` path.
8. Filesystem service sanitizes subject and prepares unique `.eml` filename.
9. Add-in saves selected email as `.eml` into target `EMAILS`.
10. If attachments exist:
    - create sanitized unique attachment folder under `EMAILS`,
    - save each attachment with unique sanitized name.
11. Workflow emits operation result summary.
12. UI shows success toast/dialog with saved paths.
13. Logging service writes operation record.
14. On recoverable errors, UI shows clear message; logs include technical detail.

## Error taxonomy and user-facing messages

| Error ID | Category | Trigger | User-facing message | Actionable guidance |
|---|---|---|---|---|
| CFG_ROOT_MISSING | Configuration | Projects root not set | "Projects root is not configured." | "Open settings and select your Projects Root folder." |
| CFG_ROOT_INVALID | Configuration | Path missing or inaccessible | "Projects root path is invalid or unavailable." | "Verify the folder exists and you have access permissions." |
| PRJ_NONE_FOUND | Discovery | No project contains `EMAILS` | "No valid projects were found." | "Ensure project folders contain an `EMAILS` subfolder, then refresh." |
| SEL_UNSUPPORTED | Selection | Non-mail item selected | "Selected item is not a supported email." | "Select a single email item and retry." |
| SEL_EMPTY | Selection | Nothing selected | "No email is selected." | "Select one email and run the command again." |
| FS_WRITE_DENIED | Filesystem | Access denied on target | "Cannot write to target folder." | "Check folder permissions or choose another project." |
| FS_PATH_INVALID | Filesystem | Invalid/too long path | "Target path is invalid." | "Shorten folder/file names or adjust project location." |
| EML_SAVE_FAILED | Outlook/File save | SaveAs for email fails | "Email could not be saved." | "Retry. If issue persists, close and reopen Outlook." |
| ATT_SAVE_FAILED | Attachment export | One or more attachments fail to save | "Some attachments could not be saved." | "Review log details and retry export." |
| OUTLOOK_BUSY | Outlook interop | COM busy/temporary failure | "Outlook is busy. Please retry." | "Wait a moment and try again." |
| UNKNOWN | General | Unhandled exception | "Unexpected error occurred." | "Retry and check logs for details." |

## Phase 1 acceptance criteria (finalized)

Phase 1 is accepted when all criteria below pass:

1. A buildable Outlook add-in skeleton exists for Classic Outlook Desktop.
2. A context command labeled `Save to Inbox2Project` is visible in email-item workflow.
3. Clicking the command invokes handler logic.
4. Handler validates selected item is a mail item and handles unsupported selections without crash.
5. Placeholder confirmation dialog appears for valid mail selection.
6. Invocation metadata is logged locally (at minimum: timestamp, item type, selection count, command invoked).
7. Setup/run instructions for Phase 1 are documented.
8. No file save logic (EML or attachments) is implemented in Phase 1.

## Phase 0 checkpoint verification evidence

Reviewer should confirm:

1. End-to-end V1 workflow is explainable from this document.
2. Constraints (Outlook variant, deployment, config path, local-only data) are explicit and realistic.
3. Phase 1 acceptance criteria are testable and unambiguous.

## Known limitations at Phase 0

1. This phase is design-only and does not include executable add-in code.
2. UI framework and exact Outlook command placement details are deferred to Phase 1 implementation.
3. Retry policy parameters for Outlook busy scenarios are deferred to hardening phase details.

## Rollback instructions

If this baseline is not approved, revert this document change and rework assumptions before Phase 1.
