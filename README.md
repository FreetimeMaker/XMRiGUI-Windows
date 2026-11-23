# XMRiGUI (Windows Native)

XMRiGUI is a free and open-source graphical user interface for the [XMRig](https://github.com/xmrig/xmrig) miner, specifically optimized for **Windows**.

This is the native **WPF (C#, .NET 8)** Windows counterpart to [XMRiGUI-Linux](https://github.com/FreetimeMaker/XMRiGUI-Linux). It does not require Python or Qt.

## Key Features

- Three independent mining profiles in tabs. Rename a profile or copy its settings to another slot after confirmation; copying stops the destination and does not start it again.
- Field-level validation before starting. Use `0` threads for automatic selection and a donation level from `0` to `100` (XMRig may impose its own minimum).
- Live hashrate and shares, a bounded time-based chart, session runtime, sample-average hashrate, and rejected-share percentage.
- Optional automatic restart after unexpected exit, with delays of 2, 4, 8, 16 and 32 seconds. Manual stop cancels pending retries. Running XMRig instances handle their own reconnects.
- Binary path and reported version, Huge Pages, MSR and permission messages. The latest 500 log lines are retained. Export redaction is best-effort; review the file before sharing it.
- Tray controls and one instance per Windows user session. Settings are stored as plain JSON in `%APPDATA%\XMRiGUI\xmrigui.json`.
- The bundled `xmrig.exe` is found in the application root or `miner/`. Review pool and wallet settings before starting; opening the Windows app without arguments never starts mining.

## Installation

### Binary Release

1. Download the latest `XMRiGUI.exe` or installer from the [Releases](https://github.com/FreetimeMaker/XMRiGUI-Windows/releases) page.
2. Extract or install to a folder of your choice.
3. Ensure `xmrig.exe` is present in the `miner/` folder or the root directory.
4. Run `XMRiGUI.exe`.

> [!WARNING]
> **Security & Antivirus**
>
> - **SmartScreen**: Windows may show a "Protected your PC" warning. Click **"More Info"** -> **"Run Anyway"**. This is common for unsigned open-source tools.
> - **Antivirus/Windows Defender**: Mining software is frequently flagged as a PUA (Potentially Unwanted Application). You may need to add an exclusion for the `XMRiGUI` folder and `xmrig.exe`.

## Build from Source

To build XMRiGUI yourself, you will need **Visual Studio 2022** (or later) with the **.NET Desktop Development** workload.

1. Clone the repository:
   ```bash
   git clone https://github.com/FreetimeMaker/XMRiGUI-Windows.git
   ```
2. Open `XMRiGUI.slnx` in a Visual Studio version that supports it, or run `dotnet build XMRiGUI.csproj` on Windows.
3. Restore NuGet packages (specifically `Newtonsoft.Json`).
4. Set the configuration to **Release**.
5. Build the solution. The output will be in `bin/Release/`.

## Command Line Options

- `start`: Attempt to start all three profiles after validating each one.
- `stop`: Stop all miners launched by this application and cancel pending retries.
- `--open` or no arguments: Show the window.
- `--close`: Hide the window without stopping miners.

On an interactive Windows desktop, run `powershell -NoProfile -File tests/Smoke-Wpf.ps1` after a debug build to check startup and single-instance hide/reopen without starting a miner.

## Contribute

### Support the Project

If you find this tool useful, consider supporting development:

- **Monero (XMR)**: `49szz88CqMWGgyDxp7VqvBS62pGLQcV4YPSBHcLwtxAXLz1Wngf8vW6is4w13Au7C2RovrTiJQaGDV5VBhFnyMBsM44Pn2P`

### Feedback

Bug reports and pull requests are welcome on [GitHub](https://github.com/FreetimeMaker/XMRiGUI).

---

**Disclaimer**: Cryptocurrency mining is hardware-intensive. Ensure your system has adequate cooling. Use this software at your own risk. The developers are not responsible for any hardware damage or lost revenue.
