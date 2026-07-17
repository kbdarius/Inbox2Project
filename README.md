# Inbox2Project
Inbox2Project is a Windows desktop automation tool that runs from Outlook and lets a user right-click a selected email to export it into local project artifacts.

## Current workflow goal (V1)

From Outlook, user selects one email and triggers `Save to Inbox2Project`.

- Always export email/thread text to a `.txt` file.
- If there are no attachments, save only the `.txt` file (no folder creation).
- If attachments exist, prompt user whether to include them.
  - If user declines: save only the `.txt` file (no folder creation).
  - If user confirms: create a sanitized subject-named folder and save both `.txt` and attachments into it.

## Phase 0 baseline

Design baseline artifact for Phase 0 is available at:

- `docs/phase-0-design-baseline.md`
