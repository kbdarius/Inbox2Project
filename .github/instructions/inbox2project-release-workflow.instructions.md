---
applyTo: "src/**/*.cs,src/**/*.csproj"
description: "Inbox2Project release workflow - ALWAYS use the scripted release path after code changes are complete and ready to ship."
---

# Inbox2Project Release Workflow

**Every time code changes are finalized in this project, run the scripted release path without waiting to be asked.**

## Source of truth

Run:

```text
Setup-Inbox2Project.bat /release
```

This command is the release workflow. It handles:

- switching to `main` and syncing from `origin/main`
- bumping the bridge version
- building and testing in `Release`
- publishing the Outlook add-in package into `artifacts/release-package`
- staging all changes
- committing the release
- pushing to `origin/main`

## When not shipping

If the goal is to update the current PC after a release, run:

```text
Setup-Inbox2Project.bat /installLocal
```

Use the non-release modes in `RELEASE.md` for other local workflows.

---

**Do not replace this with manual release steps.  
If a change is meant to ship, use the scripted release path.**
