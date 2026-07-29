# Inbox2Project Release

Use these two commands as the source of truth:

- `Setup-Inbox2Project.bat /release` to ship a release to GitHub
- `Setup-Inbox2Project.bat /installLocal` to update and install that release on the current PC

## `Setup-Inbox2Project.bat /release`

This command performs these steps automatically:

1. Switch to `main` and sync from `origin/main`.
2. Bump `src/Inbox2Project.OutlookBridge/Inbox2Project.OutlookBridge.csproj` patch version.
3. Build the solution in `Release`.
4. Run the automated tests in `Release`.
5. Publish the Outlook add-in and bridge into `artifacts/release-package`.
6. Stage all changes, commit the release, and push to `origin/main`.

## `Setup-Inbox2Project.bat /installLocal`

This command updates the current PC and performs these steps automatically:

1. Switch to `main` and sync from `origin/main`.
2. Build the solution in `Release`.
3. Run the automated tests in `Release`.
4. Force-close Outlook and known locking build/helper processes.
5. Publish the Outlook add-in and bridge into `artifacts/outlook-vsto-addin`.
6. Run the elevated installer against that published folder.
7. Verify the Outlook add-in registry entry and `LoadBehavior = 3`.
8. Reopen Classic Outlook.

## Modes

- `Setup-Inbox2Project.bat`
  Alias for `Setup-Inbox2Project.bat /installLocal`.
- `Setup-Inbox2Project.bat /installLocal`
  Sync, build, test, force-close Outlook, publish, install, and reopen Outlook on this PC.
- `Setup-Inbox2Project.bat /buildOnly`
  Sync, build, and test only.
- `Setup-Inbox2Project.bat /publishOnly`
  Sync, build, test, force-close Outlook, and publish only.
- `Setup-Inbox2Project.bat /skipInstall`
  Sync, build, test, publish, but do not install or reopen Outlook.
- `Setup-Inbox2Project.bat /release`
  Ship a release: version bump, build, test, publish to `artifacts/release-package`, commit, and push.

## Notes

- Local Outlook shutdown, add-in installation, and Outlook restart happen only in `/installLocal`. GitHub Actions cannot do those steps on your machine.
- GitHub Actions enforce build/test health and version-bump requirements for code changes, but they do not replace the local release/install flow.
- If `git switch main` or `git pull --rebase --autostash origin main` fails, resolve that git state before retrying the release.
