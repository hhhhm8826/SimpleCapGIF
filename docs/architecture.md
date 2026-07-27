# Architecture

## Projects

- `SimpleCapGIF.App`: WPF windows, state presentation, user input, and service orchestration
- `SimpleCapGIF.Core`: pixel geometry, output sizing, toolbar placement, state transitions, settings, and size estimation
- `SimpleCapGIF.Windows`: DXGI/D3D11, Win32, cursor composition, FFmpeg processes, and atomic output storage
- `SimpleCapGIF.Localization`: Windows UI-culture resolution and shared English/localized resources
- `SimpleCapGIF.Core.Tests`: platform-independent unit tests and localization contract tests
- `SimpleCapGIF.IntegrationTests`: synthetic FFV1-to-GIF/WebP encoding, failure paths, and capture lifetime tests

Dependencies point from App and Windows toward Core and Localization. Localization targets plain `net10.0`, so Core remains platform-independent while every layer can produce consistent user-facing messages.

## Capture flow

```text
Selecting
  → physical-pixel CaptureRequest
  → wait for the WPF/DWM transition to settle
  → DXGI Output Duplication
  → D3D11 video processor crop/scale/rotation
  → output-sized staging texture readback
  → bounded BGRA frame buffer + cursor composition
  → FFmpeg stdin (FFV1 Matroska)
  → stop
  → GIF palette pipeline or libwebp animation
  → partial verification
  → atomic final rename
```

The capture and encoding paths do not use unbounded queues. One frame buffer is written to FFmpeg stdin. Two consecutive writes of at least 500 ms abort safely, and FFmpeg stdout/stderr are drained concurrently.

The app waits for the recording-state windows to reach DWM composition before creating Desktop Duplication. It then retains the newest frame during a short initial settling interval. This prevents a transient excluded-window frame from becoming the permanent frame of a static recording.

## Coordinates and DPI

Core `PixelPoint`, `PixelSize`, and `PixelRect` values use integer physical pixels only. WPF DIP conversion happens at the window boundary using per-monitor DPI. Once a region fits entirely inside another monitor, the app switches its active HWND and DPI using physical cursor coordinates. Rotated monitors map coordinates into the unrotated duplication surface before GPU rotation.

`PresetRegionService` creates exact physical-pixel preset regions while preserving the center and clamping to monitor bounds. Oversized presets are rejected. `Original` preserves the region and only rounds output dimensions down when an encoder-compatible even size is required.

The selection border and toolbar are independent top-level HWNDs. Both must accept `WDA_EXCLUDEFROMCAPTURE` before recording can start.

The toolbar retains the physical top-left origin calculated in Selecting through Recording, Encoding, and Completed. Record and Stop share a fixed action slot. The localized two-row selecting layout stretches its second row to the overall toolbar width, keeping Exit aligned with the final dropdown even when translated first-row labels are longer.

## Localization

English is embedded as the neutral `en-US` resource. Eight localized satellite assemblies provide Korean, Japanese, Simplified and Traditional Chinese, Brazilian Portuguese, neutral Spanish, German, and French. `UiCultureResolver` maps Windows regional variants to these supported cultures before any WPF window is created; unsupported cultures use English. Regional number and date formatting remains independent from the UI language.

## Size estimate

Until the first sample, the estimator uses conservative format/pixel/FPS baselines. It then encodes up to the last two seconds at a maximum long edge of 320 px and 6 FPS. A separate first-frame encode splits fixed container/frame cost from variable frame-change cost, preventing static recordings from multiplying the initial cost by elapsed time.

Resolution and FPS scaling are nonlinear. Samples run at most once every two seconds with one low-priority FFmpeg thread. An EWMA smooths samples, and successful output updates a per-format/preset calibration ratio toward `actual / estimated`. Short recordings that finish before a sample do not train the conservative baseline.

## File integrity

FFmpeg executable SHA-256 values are verified before use. GIF output is checked with ffprobe for container, frame count, duration, and `NETSCAPE2.0`/`ANIMEXTS1.0` infinite-loop metadata. Animated WebP is verified directly through RIFF `ANIM/ANMF` chunks.

If libwebp optimizes an unchanged recording into one still image, SimpleCapGIF reuses the compressed frame to build an explicit animation timeline without changing pixels. Coalesced identical frames are valid when the output frame count is positive and no greater than the capture count, and total duration remains within one capture-frame interval.
