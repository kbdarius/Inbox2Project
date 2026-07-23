---
applyTo: "src/**/*.cs,src/**/*.csproj"
description: "Inbox2Project release workflow - ALWAYS follow these steps after any code change is complete and ready to ship: bump version, publish, commit, push to main, run setup."
---

# Inbox2Project Release Workflow

**Every time code changes are finalized in this project, follow ALL of these steps in order without waiting to be asked.**

## Step 1 - Bump the version

Edit `src/Inbox2Project.OutlookBridge/Inbox2Project.OutlookBridge.csproj`.
- Patch bump (x.x.**N**) for bug fixes and small tweaks
- Minor bump (x.**N**.0) for new features
- Major bump (**N**.0.0) for breaking changes

Update all three version lines together:
```xml
<Version>x.y.z</Version>
<FileVersion>x.y.z.0</FileVersion>
<AssemblyVersion>x.y.z.0</AssemblyVersion>
```

## Step 2 - Publish

Run the publish script to produce a Release build in `artifacts/outlook-vsto-addin/`:

```
powershell -NoProfile -ExecutionPolicy Bypass -File "Publish-OutlookVstoAddIn.ps1"
```

## Step 3 - Commit everything to git

Stage all changed and new files, then commit with a clear message that starts with the version tag:

```
git add -A
```

Use PowerShell for the commit so the multiline message is safe (avoids cmd interpreting newlines as commands):

```powershell
git commit -m "v1.x.x: Short summary of what changed"
```

## Step 4 - Push to main

```
git push origin main
```

## Step 5 - Run the setup bat

```
Setup-Inbox2Project.bat
```

This pulls the latest from origin, builds, publishes, and runs the installer so the updated bridge is immediately active in Outlook.

---

**Do not stop after any single step and ask the user if they want to continue.  
Complete all five steps automatically whenever code changes are ready to ship.**
