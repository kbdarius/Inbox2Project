# Inbox2Project Phase 1 Release Notes

Date: 2026-07-17
Branch: main

## Summary

Phase 1 is complete for Inbox2Project.

This release delivers the Outlook command workflow baseline and end-to-end local export behavior required for V1:
- always export selected email/thread content to a .txt file,
- no folder creation when there are no attachments,
- user choice for attachments when present,
- folder creation only when attachments are present and user confirms export.

## Delivered Scope

1. Outlook context command and handler entrypoint.
2. Selection validation for exactly one supported MailItem.
3. Settings service at %AppData%\\Inbox2Project\\settings.json.
4. Project discovery and selector abstraction (projects with EMAILS only).
5. Email/thread text extraction and .txt export with metadata header.
6. Attachment detection and user confirmation abstraction.
7. Conditional folder creation and attachment export.
8. Sanitization, collision handling, and path length safeguards.
9. Structured local operation logging.
10. Mapped error handling and user-facing messages.
11. README/run instructions and 3-path test matrix.
12. Acceptance evidence documentation.

## Commits Included

- eec5983 docs: align Phase 0 behavior and add Phase 1 plan
- 5add753 feat: implement Phase 1 export workflow services and command flow
- ad29c5c docs+dev: add harness, test matrix, and Phase 1 acceptance evidence

## Issue Closure Set

- #2 Phase 1: Add Outlook context command and handler entrypoint
- #3 Phase 1: Implement selection validation for exactly one MailItem
- #4 Phase 1: Implement settings service at %AppData%/Inbox2Project/settings.json
- #5 Phase 1: Implement project discovery and selector UI abstraction
- #6 Phase 1: Implement email/thread text extraction and .txt export
- #7 Phase 1: Implement attachment detection and confirmation prompt
- #8 Phase 1: Implement conditional folder creation and attachment export
- #9 Phase 1: Implement filename/path sanitization and collision safeguards
- #10 Phase 1: Implement structured local logging for operations
- #11 Phase 1: Implement mapped error handling and user messages
- #12 Phase 1: Update README with run instructions and 3-path test matrix
- #13 Phase 1: Validate and document acceptance criteria pass
