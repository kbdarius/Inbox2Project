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
7. **Export rule baseline:** Always export selected email/thread content to `.txt`.
8. **Attachment rule baseline:**
   - If no attachments: save only `.txt` and do not create a folder.
   - If attachments exist: prompt user to include them.
   - If user chooses **No**: save only `.txt` and do not create a folder.
   - If user chooses **Yes**: create a sanitized subject-named folder and save `.txt` plus attachments in that folder.
9. **Naming rule baseline:** Subject-based naming with sanitization and collision suffixing.

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
     - thread/text extraction,
     - `.txt` export,
     - attachment decision prompt,
     - conditional attachment export,
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
   - Creates files/folders only when required by flow.
   - Performs preflight write checks and path validation.

6. **Logging Service**
   - Writes local structured logs (success/failure, decisions, and error details).

7. **UI Layer**
   - Project selection dialog with refresh.
   - Attachment inclusion prompt when attachments exist.
   - Success/error notifications with actionable text.

## Sequence flow (V1, end-to-end)

1. User right-clicks selected Outlook item and chooses `Save to Inbox2Project`.
2. Add-in validates that exactly one supported mail item is selected.
3. Configuration service loads settings.
4. If `ProjectsRoot` is missing/invalid, UI prompts user to configure it.
5. Project discovery scans `ProjectsRoot` and lists valid projects (those with `EMAILS`).
6. User confirms project in selector (default to last selected when available).
7. Workflow resolves target `EMAILS` path.
8. Workflow extracts email/thread content and metadata for text export.
9. Filesystem service sanitizes subject and prepares unique `.txt` filename.
10. Workflow writes `.txt` in `EMAILS`.
11. Workflow checks for attachments.
12. If no attachments, operation completes (no folder creation).
13. If attachments exist, UI prompts whether to include them.
14. If prompt result is **No**, operation completes with `.txt` only (no folder creation).
15. If prompt result is **Yes**:
    - create sanitized unique subject folder under `EMAILS`,
    - move or write `.txt` into that folder,
    - save each attachment with unique sanitized name into that folder.
16. Workflow emits operation result summary.
17. UI shows success toast/dialog with saved path(s).
18. Logging service writes operation record.
19. On recoverable errors, UI shows clear message; logs include technical detail.

## Error taxonomy and user-facing messages

| Error ID | Category | Trigger | User-facing message | Actionable guidance |
|---|---|---|---|---|
| CFG_ROOT_MISSING | Configuration | Projects root not set | "Projects root is not configured." | "Open settings and select your Projects Root folder." |
| CFG_ROOT_INVALID | Configuration | Path missing or inaccessible | "Projects root path is invalid or unavailable." | "Verify the folder exists and you have access permissions." |
| PRJ_NONE_FOUND | Discovery | No project contains `EMAILS` | "No valid projects were found." | "Ensure project folders contain an `EMAILS` subfolder, then refresh." |
| SEL_UNSUPPORTED | Selection | Non-mail item selected | "Selected item is not a supported email." | "Select a single email item and retry." |
| SEL_EMPTY | Selection | Nothing selected | "No email is selected." | "Select one email and run the command again." |
| TXT_EXPORT_FAILED | Text export | `.txt` write fails | "Email text could not be saved." | "Check destination access and retry." |
| PROMPT_FAILED | Prompt/UI | Attachment prompt could not be shown or resolved | "Could not confirm attachment export choice." | "Retry command. If issue persists, restart Outlook." |
| FS_WRITE_DENIED | Filesystem | Access denied on target | "Cannot write to target folder." | "Check folder permissions or choose another project." |
| FS_PATH_INVALID | Filesystem | Invalid/too long path | "Target path is invalid." | "Shorten folder/file names or adjust project location." |
| ATT_SAVE_FAILED | Attachment export | One or more attachments fail to save | "Some attachments could not be saved." | "Review log details and retry export." |
| OUTLOOK_BUSY | Outlook interop | COM busy/temporary failure | "Outlook is busy. Please retry." | "Wait a moment and try again." |
| UNKNOWN | General | Unhandled exception | "Unexpected error occurred." | "Retry and check logs for details." |

## Phase 1 acceptance criteria (finalized)

Phase 1 is accepted when all criteria below pass:

1. A buildable Outlook add-in skeleton exists for Classic Outlook Desktop.
2. A context command labeled `Save to Inbox2Project` is visible in email-item workflow.
3. Clicking the command invokes handler logic.
4. Handler validates selection rules (exactly one item and supported mail item) and handles unsupported selections without crash.
5. Settings are loaded from `%AppData%\\Inbox2Project\\settings.json`, with required `ProjectsRoot` and optional `LastSelectedProject`.
6. Project selector lists only valid projects containing `EMAILS`.
7. Email/thread content is exported to `.txt` with metadata header.
8. Attachment presence is detected and prompt behavior follows V1 rules.
9. No-attachment path and attachment-declined path both save only `.txt` with no folder creation.
10. Attachment-confirmed path creates sanitized subject folder and saves `.txt` plus attachments.
11. Invocation, decisions, outputs, and errors are logged locally in structured form.
12. Setup/run instructions and a test matrix for the three main paths are documented.

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
