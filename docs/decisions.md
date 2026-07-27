# Decisions

## 2026-07-27 — Shared localization resources

All visible UI, accessibility, progress, error, and exception strings live in `SimpleCapGIF.Localization`. English is the neutral fallback. A startup resolver maps Windows UI-culture variants to nine supported cultures without changing regional number/date formatting. This avoids duplicated resource sets across App, Core, and Windows and keeps messages consistent when lower-layer exceptions reach the UI.

## 2026-07-27 — Documentation split

`README.md` is English user documentation and `README.ko-KR.md` is its Korean counterpart. `DEVELOPMENT.md` is the English contributor entry point and links detailed English documents under `docs`. Portable packages contain user documentation and legal notices only; developer documents remain in the repository.

## 2026-07-27 — FFmpeg distribution

Use the pinned BtbN `win64-lgpl` FFmpeg 8.1 build. Scripts and runtime verification reject unexpected hashes and GPL/nonfree/x264/x265 flags. Do not use a moving `latest` URL because it is not reproducible.

## 2026-07-27 — Animated WebP verification

The bundled FFmpeg native WebP decoder does not read `ANIM/ANMF`. Verification therefore parses RIFF chunks, loop count, frame count, and frame durations directly instead of relying on ffprobe decoding.

## 2026-07-27 — Static WebP timeline

libwebp may optimize fully identical frames into one still WebP. To preserve the v0.1.0 duration and infinite-loop contract, reuse that compressed frame bitstream in a minimal explicit `ANIM/ANMF` timeline. Do not introduce artificial pixel changes. A lower output frame count is valid when identical frames were coalesced and the duration matches.

## 2026-07-27 — Decimal megabytes

UI values use 1 MB = 1,000,000 bytes. The warning boundary is exactly 30,000,000 bytes.

## 2026-07-27 — Size estimate fixed and variable costs

Do not scale the whole low-resolution sample linearly by recording duration. Encode one frame separately to isolate the initial frame/container cost and scale only later bytes by time. Update actual-output calibration relative to the current coefficient so an already accurate coefficient does not drift back toward 1.0.

## 2026-07-27 — Settings recovery

Unreadable or malformed `settings.json` files recover to defaults instead of blocking startup. Writes complete in a temporary file in the same directory before replacement.

## 2026-07-27 — GPU resize and rotated outputs

Crop, resize, and inverse monitor rotation in the D3D11 video processor rather than scaling Desktop Duplication pixels on the CPU. Exclude first GPU-processor initialization from FPS lateness checks and start frame scheduling after the stabilized first frame.

## 2026-07-27 — Independent capture overlays

Keep the selection/recording border and settings toolbar as separate top-level HWNDs. Refuse to record unless `WDA_EXCLUDEFROMCAPTURE` succeeds for both.

## 2026-07-27 — Deterministic lifetime testing

Keep real DXGI and cursor implementations as defaults, but allow integration tests to inject a synthetic frame source and no-cursor compositor. The 60-second test uses a real FFmpeg FFV1 process and frame scheduler to detect unbounded queues or managed-memory growth.
