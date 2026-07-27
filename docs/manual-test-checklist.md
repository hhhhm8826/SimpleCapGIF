# Manual Test Checklist

When a check fails, record the Windows version, UI language, monitor layout and scale, capture region, format/preset/FPS, and FFmpeg stderr. Never attach screen pixels or user-file contents to logs.

## Operating systems

- [ ] Fresh portable ZIP on Windows 10 22H2 x64
- [ ] Fresh portable ZIP on the latest stable Windows 11 x64
- [ ] Launch and record without administrator privileges

## Localization and accessibility

- [ ] Windows English, Korean, Japanese, Simplified Chinese, Traditional Chinese, Brazilian Portuguese, Spanish, German, and French select the expected UI resources after restart
- [ ] Regional variants map correctly: `en-GB`, `es-MX`, `de-AT`, `fr-CA`, `pt-PT`, `zh-CN`, `zh-TW`, and `zh-HK`
- [ ] An unsupported Windows UI language falls back entirely to English
- [ ] Toolbar labels, tooltips, automation names, folder picker, status text, warnings, and errors use one language consistently
- [ ] GIF, WebP, FPS, MB, dimensions, file names, and raw FFmpeg details remain technically intact
- [ ] German, French, Spanish, and Portuguese labels do not clip or overlap at 100% and 200% DPI
- [ ] Japanese and Chinese glyphs render without replacement boxes
- [ ] Exit and the final second-row dropdown have the same right edge in every language
- [ ] Screen-reader names match the visible action in every language

## Monitor and DPI

- [ ] Single monitor at 100%
- [ ] 125%, 150%, and 200% scale
- [ ] Dual monitors with different scale factors
- [ ] 90°/270° rotated portrait monitor with correct region orientation and cursor position
- [ ] Secondary monitor to the left of the primary monitor with negative X coordinates
- [ ] Active monitor switches after the region moves fully inside another monitor
- [ ] A region spanning two monitors is constrained to the current monitor
- [ ] Resolution/scale changes or monitor removal produce an actionable error and clean temporary files

## Selection UI

- [ ] Region center is transparent; default border and handle outlines are black
- [ ] Dragging anywhere inside the transparent region moves it
- [ ] Eight handles resize all edges and corners
- [ ] Manual resize changes the preset to Original; moving alone preserves it
- [ ] Minimum region is 160×90 physical pixels
- [ ] Presets produce exact physical-pixel dimensions and preserve center
- [ ] Oversized presets are rejected and the previous region/dropdown are restored
- [ ] Full screen shows Original; returning restores the previous region and preset
- [ ] Toolbar remains on-screen at monitor edges, corners, and full screen
- [ ] Record and Stop retain identical physical coordinates through Selecting→Recording
- [ ] Selecting row 1 is Record, Full screen, Location, Folder, Exit; row 2 is Format, Output size, FPS
- [ ] Returning from Completed immediately restores the correct toolbar location
- [ ] Record and Stop contain icon-only circle/square visuals with localized tooltips and automation names
- [ ] Location changes and immediately persists the save folder; Folder opens it
- [ ] Completed shows Folder only; Recording and Encoding hide Location and Folder
- [ ] Main window appears in the taskbar and Exit is always visible

## Recording

- [ ] Default GIF 800×450 at 10 FPS
- [ ] WebP defaults to 15 FPS until the user explicitly chooses FPS
- [ ] 5/10/15/20/30 FPS
- [ ] 640×360, 800×450, 960×540, 1280×720, and Original
- [ ] Portrait, square, and irregular region aspect ratios
- [ ] Mouse position and shape are included
- [ ] Border and toolbar are excluded from output
- [ ] Estimated size text and red warning at 30 MB
- [ ] Static and dynamic estimates do not remain strongly biased in one direction
- [ ] No sustained managed-memory growth during a 60-second recording
- [ ] Immediately recording a static desktop after launch or after closing the folder picker does not produce a permanent black frame

## Output and failure paths

- [ ] First launch saves a selected folder immediately
- [ ] Canceling the mandatory first-launch folder picker exits
- [ ] A deleted saved folder causes the picker to appear on the next launch
- [ ] Changing the toolbar save location persists without requiring a recording
- [ ] First frame at elapsed time zero does not overflow `TimeSpan`
- [ ] Stop saves automatically to the current folder
- [ ] Same-second name collisions use `_2`, `_3`, and so on
- [ ] GIF and WebP play in Chrome, Edge, and supported Windows viewers
- [ ] Both formats loop forever
- [ ] Static WebP may coalesce frames while preserving total playback duration
- [ ] Cancellation terminates FFmpeg and deletes partial output
- [ ] Missing or modified FFmpeg prevents recording
- [ ] Output folder without write permission fails safely
- [ ] Low disk space fails safely
- [ ] Exit/taskbar close in Selecting, Recording, and Encoding leaves no FFmpeg, partial, or session directory
- [ ] Remote Desktop transition and DXGI access loss fail safely
- [ ] HDR monitor displays the localized v0.1 warning
- [ ] Protected content limitation is documented in both user READMEs

## Package

- [ ] ZIP contains `SimpleCapGIF.exe`, FFmpeg, all eight localized satellite resource folders, `README.md`, `README.ko-KR.md`, `LICENSE`, and `THIRD_PARTY_NOTICES.md`
- [ ] ZIP excludes `DEVELOPMENT.md` and the `docs` directory
- [ ] No legacy LoopCap names remain
- [ ] Packaged executable starts, records, saves, and exits from the extracted directory
