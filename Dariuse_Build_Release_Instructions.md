# Dariuse Build and Release Instructions

Use this process on every development PC after cloning the project.

## One-time setup

1. Install the required .NET SDK, Windows PowerShell, Git, Outlook desktop, and the project prerequisites.
2. Clone the repository and switch to `main`.
3. Run `Create-Inbox2ProjectShortcut.ps1` if desktop shortcuts are useful.
4. Use the shortcuts or commands below from the repository folder.

## Daily development

Build and test without installing:

```powershell
.\Setup-Inbox2Project.bat /buildOnly
```

Update the current PC with the latest `main` build. This closes Outlook, publishes the add-in, installs it, verifies the registry registration, and reopens Outlook:

```powershell
.\Setup-Inbox2Project.bat /installLocal
```

## Shipping a release

Run this only after the code is ready to ship:

```powershell
.\Setup-Inbox2Project.bat /release
```

The release flow switches to `main`, pulls the latest remote changes with rebase/autostash, increments the Outlook bridge patch version, builds the solution, runs tests, publishes the release package, commits the release, and pushes it to GitHub. It does not install the release locally or restart Outlook.

After the release push succeeds, update this PC with:

```powershell
.\Setup-Inbox2Project.bat /installLocal
```

GitHub Actions validates the pushed build and tests. Any PC with repository access can run the release because the process is stored in the repository; the person running it still needs GitHub push permission and must complete any credential or MFA prompt.

## Important rules

- Keep Outlook closed while manually changing or inspecting published add-in files.
- Do not manually edit the generated `artifacts` folders.
- Resolve build and test failures before shipping.
- Do not leave unrelated untracked files in the repository when releasing. The release script stops rather than committing them accidentally.
- If another developer pushed first, rerun `/release` so the script rebases on the current `main`.

## Troubleshooting

If the installer requests elevation, approve it. If GitHub authentication or MFA is requested, complete it and rerun the command if necessary. To inspect the installed add-in, verify the registry key:

`HKCU\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookVstoAddIn`

`LoadBehavior` should be `3` after installation.
