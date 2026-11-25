# HexTouch

[简体中文](README.zh-CN.md) | English

**Dictate, delete and insert line breaks without touching a key.** HexTouch adds three floating buttons to [Hex](https://anoma.ly/hex/) dictation on Windows. Tap once with a finger or a mouse to start, **your hands stay free while you speak**, tap again and the text lands at the cursor you left behind.

<sub><a href="https://anoma.ly/hex/">Hex</a> is a dictation tool built on a local speech model; it transcribes speech to text offline. HexTouch gives it buttons you can tap with a mouse or a finger.</sub>

It works on desktops and laptops, and is **especially useful on Windows tablets without a physical keyboard attached**. Once Hex is set up and your cursor is in a text box, the three buttons let you dictate, backspace, insert a line break and press Enter straight from the screen.

![HexTouch recording, backspace and enter buttons floating next to meeting notes in Notepad](docs/images/hextouch-notepad.png)

## Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 / 11 (development and testing done on Windows 11 26100) |
| Runtime | .NET Framework 4.x, included with Windows, nothing to install |
| Dependency | [Hex](https://anoma.ly/hex/) 3.x must be installed and able to complete one dictation |
| Privileges | No administrator rights needed when installed under your user folder |

## Quick start

1. Install and configure [Hex](https://anoma.ly/hex/), and confirm one dictation works. HexTouch drives dictation through the Hex command line; the speech model comes from Hex.
2. Download the [Windows installer (recommended)](https://github.com/cdacai/hex-touch/releases/latest/download/HexTouchSetup.exe) or the [Windows portable build](https://github.com/cdacai/hex-touch/releases/latest/download/HexTouch.exe). See the [release notes](https://github.com/cdacai/hex-touch/releases/latest).
3. Open Notepad and place the cursor. Left-click the HexTouch tray icon to show the buttons, then tap the main button and say a sentence. The main button turns red while recording.

## What HexTouch changes

HexTouch drives dictation through the Hex command line. Using a floating button as the entry point means you don't have to bind a shortcut for dictation, and you don't have to negotiate key combinations with other applications.

| Situation | With a Hex shortcut | With the HexTouch button |
|---|---|---|
| **No physical keyboard** | After detaching the tablet keyboard, the bound key is out of reach | Tap the main button to start or stop, tap backspace to delete a character, double-tap Enter for a new line |
| **Your usual keys are taken** | Hex shortcuts are global; when they collide with an IME or an editor, only one can win | Tap the button to control dictation without giving up any key binding |

## Features

- Lives in the system tray; left-click shows or hides the floating buttons
- Three buttons: **main** (record / stop), **Enter**, **backspace**
- Tap to toggle; no press-and-hold
- The floating window uses `WS_EX_NOACTIVATE`, so tapping it never steals focus from your text box
- Double-tap Enter inserts a line break (pasted from the clipboard, so a Chinese IME can't swallow `Shift+Enter`)
- Draggable, and the position is remembered
- Optional run-at-startup (checkbox in the tray menu)
- Single exe, no Python, no .NET SDK; uses the .NET Framework 4.x that ships with Windows

## Resource usage

Measured on the v0.1.2 release build on Windows 11 at 2560×1600 (200% scaling), with the floating buttons shown and idle. One sample per second, three samples, stable readings:

| Process | Working set | Private memory |
|---|---|---|
| HexTouch | 48 MiB | 30 MiB |
| Hex (concurrent, model loaded) | ~160–260 MiB | — |

The working set includes shared pages and overlaps with private memory, so don't add them together. Most of the HexTouch footprint is the .NET Framework runtime itself and cannot be reduced by changing application code; going lower would mean rewriting it in native Win32, which is not planned.

Numbers vary with hardware, Windows version and dictation state. Feel free to share your own measurements in [Issues](https://github.com/cdacai/hex-touch/issues).

## Installation

### Windows installer (recommended)

Run `HexTouchSetup.exe`:

| Action | Command |
|---|---|
| Graphical install (choose a folder) | Double-click `HexTouchSetup.exe` |
| Silent install to the default folder | `HexTouchSetup.exe --silent` |
| Silent install to a custom path | `HexTouchSetup.exe --silent --dir=D:\Tools\HexTouch` |
| Uninstall | `HexTouchUninstall.exe` in the install folder, or Settings → Apps |

The installer unpacks the main program and the uninstaller, writes the run-at-startup entry, registers an uninstall entry, and launches the app.

The install window is pre-filled with your full user path (default `%LOCALAPPDATA%\HexTouch`) and normally needs no administrator rights. You can edit the drive or type a path such as `D:\Tools\HexTouch`, or pick one with **Browse**. **Install** creates missing folders and verifies that the location is writable.

### Windows portable build

Run `HexTouch.exe` directly; nothing needs to be installed. On first run it enables run-at-startup and stores:

- the startup entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (switch it off in the tray menu)
- the first-run marker under the registry key `HKCU\Software\HexTouch`
- `hextouch.log` and `button.pos` next to the exe (log and floating button position)

It does not register an uninstall entry and does not create a Start menu shortcut.

### Building from source

Uses the `csc.exe` that ships with .NET Framework 4.x (present on Windows 10 and 11; no SDK needed).

```cmd
build.cmd
```

Produces, in order:

1. `dist\HexTouch.exe` — the main program
2. `dist\HexTouchUninstall.exe` — the uninstaller
3. `dist\HexTouchSetup.exe` — the installer, with the two files above embedded as resources

The compiler path is detected automatically under `Framework64` / `Framework`.

To regenerate the icon (Python is only used at build time):

```cmd
python tools\gen_icon.py
```

## Usage

### Start a dictation

1. Left-click the HexTouch icon in the system tray to show the three floating buttons. If you can't find the icon, expand the hidden icons on the right side of the taskbar first.
2. Click into the text position in Notepad, a chat box or a document, then tap the large circle with the waveform. When the main button turns red with a white stop square, start speaking.
3. Tap the main button again when you're done. Hex stops dictation and drops the transcription at the cursor you left.

### Editing, line breaks and sending

| Button | Gesture | Effect |
|---|---|---|
| Top-right backspace | Single tap | Sends Backspace to the active application, same as the keyboard. |
| Bottom-right Enter | Single tap | Sends Enter. Inserts a new line in Notepad; sends the message in chat apps configured to send on Enter. |
| **Bottom-right Enter** | **Two quick taps** | **Inserts a line break in input boxes that accept multi-line pastes.** Useful for starting a new paragraph while editing the same message. |

For example, in a chat app set to send on Enter, dictate the first paragraph, **double-tap Enter** for a new line, then dictate the second paragraph. After checking the text, **single-tap the same Enter button** to send the whole message.

The Enter button waits briefly for a second tap, so a single tap has a small delay. The double-tap line break borrows the clipboard to paste a newline character, then tries to restore the previous contents.

### Example: dictating prompts while reading code

Place the cursor in an AI coding assistant's input box and tap the main button, then describe requirements, reproduction steps and acceptance criteria while looking at the code. For example: "When the install folder isn't writable, keep the path the user typed, tell them to pick another folder, and allow a retry."

For longer prompts, dictate the goal first, double-tap the bottom-right Enter button for a new line, then dictate the constraints. File names, function names and error messages can be copied from the editor into place and checked before sending.

### Moving, hiding and quitting

- **Reposition**: press and drag the main button, then release; the position is saved.
- **Hide or show**: left-click the tray icon.
- **Open Hex or change startup**: right-click the tray icon and choose "唤起 Hex 窗口" (open the Hex window) or "开机自启动" (run at startup). A checkmark means it is enabled.
- **Quit**: "退出 Hex" (quit Hex) closes the dictation program and hides the buttons. "退出" (quit) closes the HexTouch tray app; reopen it from the install folder or the Start menu.

## How it works

```
Tray icon (NotifyIcon)
   │ left-click: show / hide
   ▼
Floating button window (WinForms, WS_EX_NOACTIVATE, never takes focus)
   ├─ main button  → hex dictation start / stop
   ├─ Enter button → Enter; double-tap = insert line break
   └─ backspace    → Backspace
        │
        ▼
Hex CLI (%APPDATA%\hex\bin\hex.com)
        │
        ▼
Hex local model transcription → pasted back at the original cursor
```

A few deliberate design choices:

- **Tap to toggle recording** — one tap starts, another stops; this suits finger input
- **The window uses `WS_EX_NOACTIVATE`** — tapping the buttons never takes focus away, so text and Enter reach the original input box
- **Only the Hex command line is used** — Hex itself is neither modified nor dependent on its hotkeys
- **Self-healing** — if Hex isn't running, tapping the main button launches `hex.exe` and dictation starts about 2 seconds later
- **Process check before polling** — when `hex.exe` is absent the CLI is skipped, so a hanging command can't jam the request queue; recording state is also skipped while the buttons are hidden, avoiding a process launch on every poll

## Startup and exit

### Run at startup

The "开机自启动" (run at startup) checkbox in the tray menu writes:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
  value name: HexTouchTray
  value:      <absolute path to the program>
```

You can also remove it from the command line:

```cmd
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v HexTouchTray /f
```

### Starting

```cmd
HexTouch.exe                  :: start; the tray icon appears
HexTouch.exe --show-ball      :: start and show the floating buttons immediately
```

### Quitting

| Method | Effect |
|---|---|
| Tray right-click → **退出** (quit) | Closes HexTouch. A confirmation dialog also explains how to reopen it. |
| Tray right-click → **退出 Hex** (quit Hex) | Closes only Hex; the tray app stays and the buttons are hidden. |
| Task Manager → end process | Last resort |

To reopen: double-click the program file, or use **HexTouch** in the Start menu (created by the installer only).

### Command-line arguments

| Argument | Effect |
|---|---|
| `--show-ball` | Show the floating buttons after starting |
| `--pos=x,y` | Set the initial position of the floating buttons (for debugging) |
| `--selftest` | Self-test: records for 4 seconds and exits, to verify the Hex path |

## Keeping the tray icon visible

Windows 11 puts new icons in the tray overflow area (`^`) by default. After it first appears:

Settings → Personalization → Taskbar → Other system tray icons → turn **HexTouch** on.

## Project layout

```
.
├── src/
│   ├── HexTouch.cs          # main program: tray, floating buttons, recording, Enter, backspace
│   ├── Installer.cs         # installer (embeds the main program and the uninstaller in one exe)
│   └── Uninstaller.cs       # uninstaller (copies itself to temp, deletes only files it installed)
├── assets/
│   └── app.ico              # application icon
├── tools/
│   └── gen_icon.py          # generates app.ico at build time (no Python at runtime)
├── build.cmd                # builds all three exes into dist/
└── dist/                    # build output (not committed)
```

At runtime these files appear next to the exe:

| File | Purpose |
|---|---|
| `hextouch.log` | Runtime log (cleared automatically past 512 KB) |
| `button.pos` | Remembered floating button position (created after you drag it) |

## License

MIT
