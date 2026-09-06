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
- [ ] The selection label shows only the final image dimensions once, without a source-to-output arrow
- [ ] Minimum region is 160×90 physical pixels
- [ ] Presets produce exact physical-pixel dimensions and preserve center
- [ ] Oversized presets are rejected and the previous region/dropdown are restored
- [ ] Full screen shows Original; returning restores the previous region and preset
- [ ] Full-screen selection passes mouse input through the visible border to the captured app
- [ ] Toolbar remains on-screen at monitor edges, corners, and full screen
- [ ] Record and Stop retain identical physical coordinates through Selecting→Recording
- [ ] Selecting row 1 is Record, Full screen, Location, Folder, Exit; row 2 is Format, Output size, FPS
- [ ] Returning from Completed immediately restores the correct toolbar location
- [ ] Record and Stop contain icon-only circle/square visuals with localized tooltips and automation names
- [ ] Location changes and immediately persists the save folder; Folder opens it
- [ ] Selecting shows the Settings gear; Recording, Encoding, and Completed hide it
- [ ] Completed shows a clickable result and Folder; Recording and Encoding hide Location and Folder
- [ ] Main window appears in the taskbar and Exit is always visible

## Recording

- [ ] Default GIF 800×450 at 10 FPS
- [ ] WebP defaults to 15 FPS until the user explicitly chooses FPS
- [ ] 5/10/15/20/30 FPS
- [ ] 640×360, 800×450, 960×540, 1280×720, and Original
- [ ] Portrait, square, and irregular region aspect ratios
- [ ] Mouse position and shape are included
- [ ] Cursor setting excludes the cursor when off and restores it when on, including scaled and HDR captures
- [ ] Immediate, 3-second, and 5-second starts work; countdown is centered and excluded from output
- [ ] Auto-stop and save defaults to Manual; 3/5/10/15/30-second choices persist after restart
- [ ] Each automatic duration starts from actual capture start, stops once, saves normally, and copies the completed file to the clipboard
- [ ] Manual stop or cancel before the configured duration cancels the automatic timer without a duplicate stop or save
- [ ] Countdown and recording pass mouse input through the visible border for both partial and full-screen capture
- [ ] Default Alt+F9 starts/stops while another app or a borderless full-screen game has focus
- [ ] F12, Ctrl+Shift+R, and disabled hotkey settings persist after restart
- [ ] A conflicting hotkey reports once, leaves toolbar controls usable, and can be changed in Settings
- [ ] Ctrl+Shift+F12, focused Esc, and the Stop context menu cancel without creating output
- [ ] Border and toolbar are excluded from output
- [ ] Estimated size text and red warning at 30 MB
- [ ] Sustained throughput below 90% shows actual/target FPS after the warm-up and five-second observation window
- [ ] Throughput between 50% and 90% keeps recording, and the saved GIF/WebP preserves wall-clock duration instead of playing fast
- [ ] Throughput below 50% for ten observed seconds stops safely; a recovery resets that timer
- [ ] Two consecutive FFmpeg writes of at least 500 ms still stop immediately
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
- [ ] A settings persistence failure after a successful encode reports a settings error while preserving the output file and Completed state
- [ ] Completed remains for five seconds; clicking it opens the exact output file and returns to Selecting
- [ ] Clicking a completed GIF uses its Windows file association; clicking a completed WebP opens it in Microsoft Edge
- [ ] Completing a GIF/WebP automatically places the saved file on the Windows clipboard and reports that it was copied
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
- [ ] HDR monitor records without the former warning and produces natural SDR output without washed-out colors or clipped highlights
- [ ] Protected content limitation is documented in both user READMEs

## Package

- [ ] ZIP root contains only `SimpleCapGIF.exe` and the `resources` directory
- [ ] `resources/ffmpeg` contains FFmpeg and its license; `resources/docs` contains `README.md`, `README.ko-KR.md`, `LICENSE`, `THIRD_PARTY_NOTICES.md`, and README images
- [ ] ZIP excludes `DEVELOPMENT.md` and repository developer documents
- [ ] No legacy LoopCap names remain
- [ ] Packaged executable starts, records, saves, and exits from the extracted directory
