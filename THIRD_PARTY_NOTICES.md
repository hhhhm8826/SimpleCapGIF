# Third-Party Notices

SimpleCapGIF 자체 소스 코드는 MIT License로 배포됩니다. 아래 구성 요소는 각각의 라이선스를 따릅니다.

## FFmpeg

- Version: `n8.1.2-31-g8c9502e9b0-20260726`
- Build: BtbN `ffmpeg-n8.1.2-31-g8c9502e9b0-win64-lgpl-8.1.zip`
- Mirrored binary release: <https://github.com/hhhhm8826/SimpleCapGIF/releases/tag/ffmpeg-n8.1.2-31-g8c9502e9b0-20260726>
- Archive SHA-256: `923522df4e21c84cf6bd533ad690ea9b134087b38a95535a35abd786c25445c9`
- `ffmpeg.exe` SHA-256: `e674aa31bc9e6f56f955c7ce87a194a5f949f67545b59f723f155053acb1269d`
- `ffprobe.exe` SHA-256: `ac9bf61f6f6f642e7f655e86ff60c7fe5670eebd16c18de3a9bbf81af03c50db`
- Corresponding FFmpeg source: <https://git.ffmpeg.org/gitweb/ffmpeg.git/commit/8c9502e9b0>
- Build project: <https://github.com/BtbN/FFmpeg-Builds>
- License: GNU Lesser General Public License 3.0

배포 빌드는 `--enable-gpl`, `--enable-nonfree`, `--enable-libx264`, `--enable-libx265`를 사용하지 않습니다. `--enable-libwebp`를 사용하며 FFV1, GIF, palettegen, paletteuse, libwebp 애니메이션 인코더를 포함합니다. 정확한 전체 configure 문자열은 배포본의 `ffmpeg -version` 출력과 `ffmpeg/LICENSE.txt`에서 확인할 수 있습니다.

FFmpeg는 SimpleCapGIF과 별개의 저작물이며 SimpleCapGIF의 MIT License가 적용되지 않습니다. Portable ZIP에는 FFmpeg 원본 `LICENSE.txt`가 포함됩니다.

## Vortice.Windows

- Packages: `Vortice.Direct3D11` 3.8.3, `Vortice.DXGI` 3.8.3
- Source: <https://github.com/amerkoleci/Vortice.Windows>
- License: MIT

## xUnit.net and coverlet

테스트에서 xUnit.net(Apache-2.0)과 coverlet(MIT)을 사용합니다. 이 테스트 도구는 최종 Portable ZIP에 포함되지 않습니다.
