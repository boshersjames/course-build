# Set up the PC so Claude can run Unity there (about 10 minutes, once)

## 1. Install Claude Code
1. Open **PowerShell** (Start menu, type PowerShell, press Enter). A normal window is fine, not "as administrator".
2. Paste this and press Enter:
   ```
   irm https://claude.ai/install.ps1 | iex
   ```
3. Close PowerShell and open it again, then check it worked:
   ```
   claude --version
   ```
   If it says `claude` is not recognised, sign out of Windows and back in (the PATH change needs a new session).
   Claude Code on Windows also needs Git for Windows. You already have Git for the repo, so this should be fine.
   If it asks for Git Bash, install it from https://git-scm.com/download/win with the default options.

## 2. Get the automation branch
```
cd $HOME\Documents\tierra-rejada
git fetch
git checkout claude/unity-automation
```

## 3. Sign in once
```
claude
```
Pick the login with your claude.ai account (the same one you use for this project). The browser opens to confirm.
When it shows the Claude prompt, type `/exit`.

## 4. Start Remote Control
In the same folder:
```
cd $HOME\Documents\tierra-rejada
claude remote-control
```
Leave this window open and keep the PC awake (Settings > System > Power: Sleep "Never" while plugged in).
The PC then appears as a device in the project, and a Claude session can run on it from the app, even from your phone.

## 5. First run (Claude can do this part once Remote Control is up)
1. Close Unity.
2. `.\automation\Run-CourseAutomation.ps1 -SyncTools -Steps none`
   This copies CourseAutomation.cs into the project, opens the scene in batch mode, checks it and takes the screenshots.
   It changes nothing in the scene. It is a safe first test.
3. Read the summary it prints (also in `C:\GSP\GSPBaseProject_v4_2025_12_02\AutomationOutput\`).

Claude will ask before it runs commands. You can approve each one from your phone, or pick "don't ask again" for
PowerShell in the repo folder.
