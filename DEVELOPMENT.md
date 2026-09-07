# Developing SimpleCapGIF

This document is the entry point for building, testing, packaging, and contributing to SimpleCapGIF.

## Requirements

- Windows 10 or 11 x64
- .NET 10 SDK
- PowerShell 7 or Windows PowerShell 5.1
- Internet access for the first FFmpeg dependency fetch

## Build and test

```powershell
./scripts/verify-text-format.ps1
dotnet restore SimpleCapGIF.sln
dotnet build SimpleCapGIF.sln -c Release --no-restore
dotnet test SimpleCapGIF.sln -c Release --no-build --filter "Category!=Soak"
```

The app build automatically downloads the pinned FFmpeg dependency when it is missing, verifies its checksums and required codecs, and copies the runtime files into the app output's `ffmpeg` directory.

The integration suite uses the pinned FFmpeg binaries and includes synthetic GIF/WebP encoding and failure cleanup. The 60-second capture-lifetime soak test runs in the weekly `soak` workflow or manually with:

```powershell
dotnet test tests/SimpleCapGIF.IntegrationTests/SimpleCapGIF.IntegrationTests.csproj -c Release --filter "Category=Soak"
```

## Package

```powershell
./scripts/package.ps1
```

The result is `artifacts/package/SimpleCapGIF-v0.3.0-win-x64.zip`. Its root contains only the self-contained single-file `SimpleCapGIF.exe` and a `resources` directory. Pinned FFmpeg binaries are under `resources/ffmpeg`; English and Korean user READMEs, image assets, licenses, and third-party notices are under `resources/docs`. Developer documentation is repository-only.

## Repository map

- `SimpleCapGIF.App`: WPF windows, user input, and orchestration
- `SimpleCapGIF.Core`: platform-independent geometry, settings, state, placement, and size estimation
- `SimpleCapGIF.Windows`: DXGI/D3D11 capture, Win32 integration, FFmpeg, and session storage
- `SimpleCapGIF.Localization`: culture resolution and shared `.resx` resources
- `tests`: platform-independent and Windows integration tests
- `tools/SimpleCapGIF.ReadmeRenderer`: off-screen rendering of the real WPF toolbar for the user READMEs
- `docs`: architecture, decisions, and manual release checks

See [architecture.md](docs/architecture.md), [decisions.md](docs/decisions.md), and the [manual test checklist](docs/manual-test-checklist.md).

## FFmpeg integrity

The FFmpeg version and SHA-256 values are pinned in `FfmpegToolchain` and verified before recording. `scripts/fetch-ffmpeg.ps1` downloads only the expected archive; `scripts/verify-ffmpeg.ps1` verifies the archive contents and licensing flags. Do not replace the binaries or update hashes without recording the decision and rerunning the full encoding suite.

## Localization

English (`Resources/Strings.resx`) is the canonical and fallback language. Supported localized resources are `ko-KR`, `ja-JP`, `zh-Hans`, `zh-Hant`, `pt-BR`, neutral `es`, `de-DE`, and `fr-FR`.

When adding or changing a string:

1. Add the key to the English resource and every localized resource.
2. Preserve composite-format placeholders such as `{0}` and `{1}` exactly.
3. Keep technical tokens such as GIF, WebP, FPS, MB, FFmpeg, dimensions, and file names unchanged unless the unit has an established localized form.
4. Route visible XAML text, tooltips, automation names, progress text, errors, and exceptions through `AppStrings`.
5. Run resource parity tests and inspect long German, French, and Spanish layouts at 100% and 200% DPI.

The app chooses a supported UI culture from the Windows UI culture at startup. Unsupported languages use English. There is no in-app language setting and no live language switch.

## README images

The logo in `images/simplecapgif-logo.png` is copied from the application icon. Regenerate the English toolbar preview after visible toolbar changes:

```powershell
dotnet run --project tools/SimpleCapGIF.ReadmeRenderer/SimpleCapGIF.ReadmeRenderer.csproj -c Release -- en-US images/simplecapgif-app.png
dotnet run --project tools/SimpleCapGIF.ReadmeRenderer/SimpleCapGIF.ReadmeRenderer.csproj -c Release -- ko-KR images/simplecapgif-app.ko-KR.png
```

The renderer loads `ToolbarWindow.xaml` and its localized bindings directly, so the preview stays aligned with the shipped controls rather than a separately drawn mockup. Inspect the PNG before packaging; `scripts/package.ps1` verifies that both README images are present in the portable ZIP.

## Release verification

Run the full automated suite, complete every applicable item in the manual checklist, build the portable ZIP from a clean output folder, verify every supported language in the packaged single-file app and inspect the document allowlist, then launch `SimpleCapGIF.exe` from the packaged directory. Record the ZIP SHA-256 with the release.
