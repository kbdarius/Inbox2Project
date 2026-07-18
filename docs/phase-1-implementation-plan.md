# Inbox2Project - Phase 1 Implementation Plan

## Objective

Deliver a buildable Outlook-focused implementation for V1 behavior:
- right-click command in Outlook,
- guaranteed `.txt` export of selected email/thread,
- conditional attachment export based on explicit user decision,
- no folder creation unless attachments exist and user confirms export.

## Scope and assumptions

1. Implementation targets Windows and Classic Outlook Desktop integration boundaries.
2. Phase 1 includes full workflow logic and documentation.
3. Data remains local only.
4. Existing baseline constraints in Phase 0 remain in force.

## Deliverables mapped to Phase 1 task list

1. Add Outlook context command `Save to Inbox2Project` and handler entrypoint.
2. Implement selection validation (exactly one item, supported `MailItem`, user-friendly errors).
3. Implement settings service at `%AppData%\\Inbox2Project\\settings.json` (`ProjectsRoot` required, `LastSelectedProject` optional).
4. Implement project discovery + selector UI (only folders containing `EMAILS`).
5. Implement email/thread text extraction and `.txt` export with metadata header.
6. Implement attachment detection + confirmation prompt.
7. Implement conditional folder creation + attachment export based on prompt decision.
8. Implement filename/path sanitization, collision handling, and length safeguards.
9. Implement structured local logging for invocation, decisions, outputs, and errors.
10. Implement mapped error handling/user messages (config, selection, txt export, prompt, filesystem, attachment, Outlook busy, unknown).
11. Update README/run instructions and add test matrix for 3 paths (no attachments / attachments+No / attachments+Yes).
12. Validate and document Phase 1 acceptance criteria pass.

## Implementation chunks

### Chunk A - Solution skeleton and command entrypoint

Covers tasks: 1, part of 2, part of 9.

Output:
- Buildable solution/project structure.
- Command handler entrypoint abstraction and invocation log event.

### Chunk B - Core domain services

Covers tasks: 2, 3, 4, 8, part of 10.

Output:
- Selection validation service.
- Settings read/write service.
- Project discovery/selection service.
- Path/name sanitization and collision-safe filesystem helper.

### Chunk C - Export workflow implementation

Covers tasks: 5, 6, 7, part of 9, part of 10.

Output:
- Text extraction and `.txt` writer.
- Attachment detection and prompt handling.
- Conditional folder behavior and attachment export.
- Workflow-level structured logging and mapped exception handling.

### Chunk D - Docs, tests, and acceptance evidence

Covers tasks: 11, 12 and completion checks for all tasks.

Output:
- Updated README with run instructions and test matrix.
- Acceptance criteria checklist and execution evidence document.

## Proposed repository structure additions

- `src/Inbox2Project/`
  - `Program.cs` (dev harness entry)
  - `Core/` (workflow + contracts)
  - `Services/` (settings, discovery, filesystem, logging, export)
  - `Interop/` (Outlook abstractions/adapters)
  - `UI/` (selection and prompt abstractions)
  - `Models/` (request/result/error types)
- `docs/phase-1-acceptance-evidence.md`

## Completion definition

Phase 1 is complete when:
1. All 12 tasks are implemented in code/docs.
2. Main workflow supports and logs all three functional paths.
3. Acceptance evidence confirms each Phase 0 acceptance criterion.

## Follow-on native Outlook integration

The supported Classic Outlook integration is the .NET Framework 4.8 managed COM add-in
at `src/Inbox2Project.OutlookVstoAddIn`. It implements the official
`IDTExtensibility2` contract, subscribes to Outlook's item context-menu event, and
launches the co-located published bridge. `Publish-OutlookVstoAddIn.ps1` and
`Install-OutlookVstoAddIn.ps1` produce and register the deployment directory.

The earlier .NET 8 in-process COM host was retired after Outlook reported a native
`coreclr.dll` module-version crash. It is no longer part of the solution or supported
installation path.
