# Inbox2Project
Inbox2Project is a Windows desktop automation tool that runs from Outlook and lets a user right-click a selected email to export it into local project artifacts.

## Current workflow behavior (V1)

From Outlook, user selects one email and triggers `Save to Inbox2Project`.

- Always export email/thread text to a `.txt` file.
- If there are no attachments, save only the `.txt` file (no folder creation).
- If attachments exist, prompt user whether to include them.
  - If user declines: save only the `.txt` file (no folder creation).
  - If user confirms: create a sanitized subject-named folder and save both `.txt` and attachments into it.

## Phase documents

- Phase 0 baseline: `docs/phase-0-design-baseline.md`
- Phase 1 implementation plan: `docs/phase-1-implementation-plan.md`
- Phase 1 acceptance evidence: `docs/phase-1-acceptance-evidence.md`

## Repository layout

- `src/Inbox2Project` - core command, validation, workflow, settings, discovery, path safety, logging.
- `src/Inbox2Project.DevHarness` - runnable harness for validating the 3 required export paths.

## Build and run

### Prerequisites

- Windows 10/11
- .NET SDK 8.0+

### Build

```powershell
dotnet build Inbox2Project.sln
```

### Run validation harness

```powershell
dotnet run --project src/Inbox2Project.DevHarness -- no-attachments
dotnet run --project src/Inbox2Project.DevHarness -- attachments-no
dotnet run --project src/Inbox2Project.DevHarness -- attachments-yes
```

### Try from Outlook selection (bridge)

You can run the bridge while Outlook is open and one email is selected.

```powershell
dotnet run --project src/Inbox2Project.OutlookBridge
```

Attachment decision modes:

```powershell
dotnet run --project src/Inbox2Project.OutlookBridge -- --include-attachments ask
dotnet run --project src/Inbox2Project.OutlookBridge -- --include-attachments no
dotnet run --project src/Inbox2Project.OutlookBridge -- --include-attachments yes
```

Notes:
- This bridge reads selection from active Classic Outlook explorer window.
- Select exactly one mail item before running.
- This is a practical bridge for testing; full right-click add-in packaging is a separate integration step.

### Install the Outlook right-click add-in

The repository now includes a native COM add-in boundary for Classic Outlook. It adds
`Save to Inbox2Project` to the Outlook explorer context menu and starts the published
bridge from the same installation directory.

Publish both components into one directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\Publish-OutlookAddIn.ps1
```

Install the current user's Outlook registration from that published directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\src\Inbox2Project.OutlookAddIn\install-addin.ps1 -AddInDirectory <published-directory>
```

Restart Classic Outlook after installation. The add-in and Outlook must use compatible
process bitness. Unregister the COM host before removing the directory:

```powershell
regsvr32.exe /u <published-directory>\Inbox2Project.OutlookAddIn.comhost.dll
```

The add-in registration is per-user under `HKCU`; no administrator elevation is
required. The bridge remains available for direct validation and troubleshooting.

## Settings file

Path:

`%AppData%\\Inbox2Project\\settings.json`

Example:

```json
{
  "ProjectsRoot": "C:\\Users\\<user>\\AppData\\Roaming\\Inbox2Project\\Projects",
  "LastSelectedProject": "C:\\Users\\<user>\\AppData\\Roaming\\Inbox2Project\\Projects\\SampleProject"
}
```

Rules:
- `ProjectsRoot` is required.
- `LastSelectedProject` is optional.

## Phase 1 test matrix

| Scenario | Input mode | Expected output | Expected folder behavior |
|---|---|---|---|
| No attachments | `no-attachments` | One `.txt` file under `EMAILS` | No folder creation |
| Attachments + user No | `attachments-no` | One `.txt` file under `EMAILS` | No folder creation |
| Attachments + user Yes | `attachments-yes` | One subject folder with `.txt` + attachments | Folder is created |
