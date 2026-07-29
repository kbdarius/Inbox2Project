# Inbox2Project Agent Notes

For any change that is intended to ship, do not run release steps manually.

Use:

```text
Setup-Inbox2Project.bat /release
```

That scripted release path is the source of truth for:

- version bumping
- build and test validation
- publishing the add-in package into `artifacts/release-package`
- staging, committing, and pushing release changes

To update the current PC after a release, use:

```text
Setup-Inbox2Project.bat /installLocal
```

That local install path is the source of truth for:

- force-closing Outlook before publish/install
- publishing the add-in package
- running the elevated installer
- reopening Outlook

If a task is not meant to ship yet, use a non-release mode from `RELEASE.md`.
