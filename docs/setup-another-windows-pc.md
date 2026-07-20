# Inbox2Project setup on another Windows PC

This runbook is written for an AI agent that has permission to work on the target PC. Follow the steps in order. Do not delete, rename, or directly edit any existing Outlook profile, `.pst`, or `.ost` file. Do not create a replacement Outlook profile unless the existing profile fails the checks in this guide.

## Target result

In **Classic Outlook for Windows**, right-clicking one selected email shows **Save to Inbox2Project**. Clicking it opens the project selector and exports the message text, with an optional attachment-selection step.

## Important constraints

- This integration works with **Classic Outlook**, not the new Outlook app.
- The current installer supports **64-bit Outlook only**.
- The add-in registration is per Windows user. Install it while signed in as the person who will use it.
- Administrator elevation is required for COM registration. The user must approve the Windows UAC prompt.
- Use a permanent local checkout such as `C:\Tools\Inbox2Project`. Do not move or delete it after installation; Outlook's COM registration points to the published DLL by its full path.
- The add-in is registered for Classic Outlook as a whole, so it is available in every Classic Outlook profile belonging to the same Windows user.

## 1. Record the starting state

Before changing anything, record:

- Windows version.
- Outlook edition and whether the **Try the new Outlook** switch is Off.
- Outlook bitness.
- The active Outlook profile and whether its inbox opens normally.
- Existing repository location, if Inbox2Project is already present.

Check Office bitness in a normal PowerShell window:

```powershell
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' |
    Select-Object Platform, ClientVersionToReport
```

Continue only when `Platform` is `x64`. If it is `x86`, stop and report that the current installer does not support that Outlook installation.

Confirm Classic Outlook exists:

```powershell
Test-Path 'C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE'
```

If Outlook is already open, confirm its inbox works before installing anything. A pre-existing Outlook/profile failure should be diagnosed separately from Inbox2Project.

## 2. Install prerequisites

Required software:

- Git for Windows.
- .NET 8 SDK (not only the runtime).
- 64-bit Classic Outlook for Windows.
- .NET Framework 4.8 runtime, normally already present with supported Office installations.

Verify Git and .NET:

```powershell
git --version
dotnet --list-sdks
```

At least one SDK beginning with `8.` must be listed. If a prerequisite is missing, ask the user before downloading or installing software.

## 3. Clone or update the repository

Use a permanent local directory. For a fresh installation:

```powershell
New-Item -ItemType Directory -Path 'C:\Tools' -Force | Out-Null
Set-Location 'C:\Tools'
git clone https://github.com/kbdarius/Inbox2Project.git
Set-Location 'C:\Tools\Inbox2Project'
git switch main
git pull --ff-only origin main
```

For an existing checkout:

```powershell
Set-Location 'C:\Tools\Inbox2Project'
git status --short
git switch main
git pull --ff-only origin main
```

If `git status --short` reports local changes, do not discard or overwrite them. Report them to the user before continuing.

## 4. Build and validate the source

```powershell
Set-Location 'C:\Tools\Inbox2Project'
dotnet build .\Inbox2Project.sln -c Release
```

Do not continue if the build fails. Record the complete error output.

Optional core workflow validation:

```powershell
dotnet run --project .\src\Inbox2Project.DevHarness -- no-attachments
dotnet run --project .\src\Inbox2Project.DevHarness -- attachments-no
dotnet run --project .\src\Inbox2Project.DevHarness -- attachments-yes
```

## 5. Publish the Outlook add-in and bridge

```powershell
Set-Location 'C:\Tools\Inbox2Project'
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-OutlookVstoAddIn.ps1
```

Confirm these two primary files exist:

```powershell
$published = 'C:\Tools\Inbox2Project\artifacts\outlook-vsto-addin'
Test-Path "$published\Inbox2Project.OutlookVstoAddIn.dll"
Test-Path "$published\Inbox2Project.OutlookBridge.exe"
```

Both commands must return `True`.

## 6. Close Outlook safely

Ask the user to save any open draft messages and close every Outlook window. Then verify Outlook has exited:

```powershell
Get-Process OUTLOOK -ErrorAction SilentlyContinue
```

If an Outlook process remains, do not force-close it until the user confirms there are no unsaved drafts or dialogs.

## 7. Install with administrator elevation

Run the installer from the user's normal PowerShell session. This opens a UAC prompt that the user must approve:

```powershell
$published = 'C:\Tools\Inbox2Project\artifacts\outlook-vsto-addin'
$installer = Join-Path $published 'Install-OutlookVstoAddIn.ps1'
$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$installer`" -AddInDirectory `"$published`""
Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait
```

The installer performs these scoped actions:

- Registers `Inbox2Project.OutlookVstoAddIn.dll` using 64-bit .NET Framework `RegAsm.exe` and its permanent published path.
- Creates the per-user Outlook add-in key.
- Sets `LoadBehavior` to `3` so Outlook loads the add-in at startup.
- Removes only Outlook resiliency entries whose payload names Inbox2Project.
- Removes the retired conflicting `Inbox2Project.OutlookAddIn` registration, if present.

It must not remove unrelated add-ins or clear Outlook's entire resiliency registry area.

## 8. Verify registration before opening Outlook

In a normal, non-elevated PowerShell window:

```powershell
$key = 'HKCU:\Software\Microsoft\Office\Outlook\Addins\Inbox2Project.OutlookVstoAddIn'
Get-ItemProperty $key | Select-Object FriendlyName, Description, LoadBehavior
```

Expected values:

- `FriendlyName`: `Inbox2Project`
- `LoadBehavior`: `3`

Also verify that the registered files still exist in:

```text
C:\Tools\Inbox2Project\artifacts\outlook-vsto-addin
```

## 9. Start and test Classic Outlook

Start Outlook normally. Keep the **Try the new Outlook** switch Off.

1. Wait until the inbox finishes opening.
2. Select exactly one harmless test email.
3. Right-click the selected email.
4. Confirm **Save to Inbox2Project** appears near the top of the shortcut menu.
5. Click it.
6. In the project selector, use **Add Project** to add an existing project root folder. Give it a helpful nickname if desired.
7. Select that project and choose **Save to Selected Project**.
8. Confirm the completion window reports success.

For an email without attachments, verify a `.txt` file appears under:

```text
<selected-project>\EMAILS
```

For a harmless email with attachments, verify the attachment-selection dialog appears. Test both behaviors if practical:

- Select no attachments: only the message `.txt` is exported.
- Select attachments: a subject-named folder is created containing the `.txt` and selected attachments.

Do not use financial, medical, legal, or otherwise sensitive email for installation testing.

## 10. Verify the add-in inside Outlook

If the command appears, the integration is complete. Optionally confirm its status in:

```text
File > Options > Add-ins > Active Application Add-ins
```

Inbox2Project should be active. Leave Outlook open for the user when testing is complete.

## Settings and project list

Per-user settings are stored at:

```text
%AppData%\Inbox2Project\settings.json
```

A current settings file resembles:

```json
{
  "ProjectsRoot": "C:\\Users\\<user>\\AppData\\Roaming\\Inbox2Project\\Projects",
  "LastSelectedProject": "D:\\Projects\\ExampleProject",
  "SavedProjects": [
    {
      "Name": "Example Project",
      "ProjectPath": "D:\\Projects\\ExampleProject"
    }
  ]
}
```

Prefer managing projects through the project selector UI. Do not copy another PC's `settings.json` without correcting user-specific paths.

## Troubleshooting decision tree

### Outlook opens, but the right-click command is missing

1. Confirm this is Classic Outlook and Outlook is 64-bit.
2. Close Outlook completely.
3. Confirm the add-in registry key exists and `LoadBehavior` is `3`.
4. Republish and rerun the elevated installer.
5. Restart Outlook and check `File > Options > Add-ins`.
6. If listed under Disabled or Inactive add-ins, enable Inbox2Project and restart Outlook.

Do not reintroduce the old `ItemContextMenuDisplay`/CommandBar implementation. The working implementation uses `Office.IRibbonExtensibility`, ribbon ID `Microsoft.Outlook.Explorer`, and context menu ID `ContextMenuMailItem`.

### The command appears, but clicking it does nothing

1. Confirm `Inbox2Project.OutlookBridge.exe` exists beside the add-in DLL in the published directory.
2. Confirm the repository/published directory was not moved after installation.
3. If it was moved, publish again and rerun the installer using the new permanent path.
4. Select exactly one actual email item and retry.

### Outlook reports no active selection or no active explorer

- Open the main Outlook inbox window.
- Select exactly one email, not a folder, calendar item, or empty space.
- Retry the right-click command.

### Outlook itself will not open or the inbox is broken

First determine whether the problem existed before installing Inbox2Project.

1. Close Outlook.
2. Uninstall Inbox2Project using the procedure below.
3. Retry Outlook. If it remains broken, treat it as an Outlook profile/Office issue rather than an add-in issue.
4. Try Microsoft's normal Office repair and Outlook profile UI before editing the registry.
5. If a temporary recovery profile is necessary, use **Control Panel > Mail > Show Profiles > Add**, add one account, and test Outlook there.
6. Launch a named profile when needed:

```powershell
& 'C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE' /profile 'RecoveryProfileName'
```

Because the add-in registration is per Windows user, it should load in both the original and recovery profiles. Preserve the original profile. Never delete its mail data as part of add-in troubleshooting.

If Outlook says creating a new `.pst` file is not allowed but the inbox opens, dismiss the warning and test the add-in. Do not alter data-file policies without explicit user direction.

### Outlook disables the add-in after a crash

Close Outlook, rerun the elevated installer, and restart Outlook. The installer resets only Inbox2Project-specific resiliency entries. If Outlook repeatedly disables it, collect the Windows Application event log entries for Outlook and Inbox2Project before making further changes.

## Updating Inbox2Project later

Keep the same permanent checkout path:

```powershell
Set-Location 'C:\Tools\Inbox2Project'
git status --short
git pull --ff-only origin main
dotnet build .\Inbox2Project.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-OutlookVstoAddIn.ps1
```

Then close Outlook and rerun the elevated installation command from step 7. Restart Outlook and repeat the basic right-click test.

## Uninstalling safely

Save drafts and close Outlook. Then run the repository's uninstall script from an elevated PowerShell window:

```powershell
$repository = 'C:\Tools\Inbox2Project'
$published = Join-Path $repository 'artifacts\outlook-vsto-addin'
$uninstaller = Join-Path $repository 'Uninstall-OutlookVstoAddIn.ps1'
$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$uninstaller`" -AddInDirectory `"$published`""
Start-Process powershell.exe -Verb RunAs -ArgumentList $arguments -Wait
```

Restart Outlook. Uninstalling removes only the Inbox2Project COM registration and Outlook add-in key; exported project files and `%AppData%\Inbox2Project` settings remain.

## Completion report for the AI agent

At the end, report:

- Target PC Outlook edition and bitness.
- Repository path and installed commit (`git rev-parse --short HEAD`).
- Build result.
- Published directory.
- Registry `LoadBehavior` result.
- Whether the command appeared in the right-click menu.
- Whether a no-attachment export succeeded and its output path.
- Whether the attachment-selection dialog appeared.
- Any warnings encountered, without exposing email contents or sensitive paths unnecessarily.

