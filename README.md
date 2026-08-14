<p align="center">
  <img src="images/simplecapgif-logo.png" alt="SimpleCapGIF logo" width="144">
</p>

<h1 align="center">SimpleCapGIF</h1>

<p align="center">
  English | <a href="README.ko-KR.md">한국어</a>
</p>

SimpleCapGIF is a minimal region recorder for GIF and Animated WebP on Windows 10/11 x64. Select an area, press Record, then Stop; the result is saved directly to your chosen folder without an editor or upload step.

<p align="center">
  <img src="images/simplecapgif-app.png" alt="SimpleCapGIF two-row recording toolbar" width="720">
</p>

## Get started

1. Download and extract `SimpleCapGIF-v0.1.2-win-x64.zip`. Keep the `resources` folder next to `SimpleCapGIF.exe`.
2. Run `SimpleCapGIF.exe`. On first launch, choose a save folder. Canceling closes the app.
3. Drag inside the black border to move the region, or use the eight handles to resize it.
4. Choose GIF/WebP, output size, and FPS from the second toolbar row.
5. Press the circular Record icon, then the square Stop icon in the same position. The animation is saved automatically.

## Controls

- **Capture area:** Drag inside the black border to move it and use the eight handles to resize it. **Full screen** captures the active monitor; returning restores the previous selection.
- **Output:** Choose GIF/WebP, output size, and FPS. The default is GIF at `800×450 · 10 FPS`; WebP uses 15 FPS unless you selected an FPS manually.
- **Resolution presets:** Resize the selected area to the exact physical-pixel size shown. Manual resizing switches to **Original**; moving the area keeps the preset.
- **Record and save:** Use the circular button or **F12** to start. Use the square button or press **F12** again to stop and save automatically.
- **Cancel without saving:** Press **Ctrl+Shift+F12**, right-click Stop, or press **Esc** while SimpleCapGIF has focus.
- **Recording settings:** Use the gear menu to include or hide the cursor, choose an immediate, 3-second, or 5-second delay, and change or disable the global hotkey.
- **Files:** **Location** changes the save folder and **Folder** opens it. Click the completion message within five seconds to open the saved file.

## Languages

The app follows the Windows UI language when it starts. It supports English, Korean, Japanese, Simplified Chinese, Traditional Chinese, Brazilian Portuguese, Spanish, German, and French. Unsupported languages fall back to English.

## Storage and privacy

- Settings: `%LocalAppData%\SimpleCapGIF\settings.json`
- Temporary recording data: `%LocalAppData%\SimpleCapGIF\Temp\{session-id}`
- The initial folder picker opens at `%USERPROFILE%\Pictures\SimpleCapGIF` when no saved location exists.
- SimpleCapGIF makes no network requests and has no account, telemetry, upload, or automatic-update feature.
- Screen pixels and user-file contents are not written to logs.

## Limitations

- A capture region must fit entirely within one monitor.
- Audio, webcam capture, window tracking, editing, and MP4 export are not included.
- You can record a display with HDR turned on. SimpleCapGIF automatically adjusts very bright areas for GIF and WebP so they are less likely to look washed out or lose detail. The saved file is standard SDR, not HDR.
- DRM-protected content may appear black.

## License and development

SimpleCapGIF is released under the [MIT License](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for FFmpeg and other bundled components.

For building, testing, packaging, architecture, and localization contributions, see [DEVELOPMENT.md](DEVELOPMENT.md).
