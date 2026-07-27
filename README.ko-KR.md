<p align="center">
  <img src="images/simplecapgif-logo.png" alt="SimpleCapGIF 로고" width="144">
</p>

<h1 align="center">SimpleCapGIF</h1>

<p align="center">
  <a href="README.md">English</a> | 한국어
</p>

SimpleCapGIF은 Windows 10/11 x64용 간단한 GIF·Animated WebP 영역 녹화기입니다. 편집기나 업로드 과정 없이 영역을 정하고 녹화와 중단을 누르면 지정한 폴더에 바로 저장합니다.

<p align="center">
  <img src="images/simplecapgif-app.ko-KR.png" alt="SimpleCapGIF의 두 줄 녹화 도구 모음" width="720">
</p>

## 시작하기

1. `SimpleCapGIF-v0.1-win-x64.zip`을 내려받아 압축을 풉니다.
2. `SimpleCapGIF.exe`를 실행합니다. 최초 실행에서는 저장 폴더를 선택하며, 취소하면 앱이 종료됩니다.
3. 검정 테두리 안쪽을 드래그해 영역을 이동하고 8개의 핸들로 크기를 조절합니다.
4. 도구 모음 둘째 줄에서 GIF/WebP, 출력 크기, FPS를 선택합니다.
5. 원형 녹화 아이콘을 누른 뒤 같은 위치의 사각형 중단 아이콘을 누릅니다. 결과는 자동으로 저장됩니다.

## 조작 방법

- **전체 화면**은 활성 모니터 전체를 녹화합니다. 영역으로 복귀하면 이전 영역과 프리셋이 복원됩니다.
- **위치**는 저장 폴더를 변경하고 **폴더**는 현재 저장 폴더를 엽니다.
- 해상도 프리셋은 영역을 표시된 물리 픽셀 크기로 맞춥니다. 직접 크기를 조절하면 **원본**으로 바뀌며, 이동만 하면 프리셋이 유지됩니다.
- 기본값은 GIF `800×450 · 10 FPS`입니다. WebP는 FPS를 직접 지정하지 않은 경우 15 FPS를 사용합니다.
- 예상 결과가 30MB 이상이면 용량 표시가 빨간색으로 바뀝니다. SimpleCapGIF은 품질을 임의로 낮추지 않고 경고합니다.
- 도구 모음의 **×** 또는 작업표시줄 닫기로 종료할 수 있습니다. 실행 중인 FFmpeg와 임시 파일을 먼저 정리합니다.

## 지원 언어

앱을 시작할 때 Windows UI 언어를 따릅니다. 영어, 한국어, 일본어, 중국어 간체·번체, 포르투갈어(브라질), 스페인어, 독일어, 프랑스어를 지원하며 그 밖의 언어는 영어로 표시됩니다.

## 저장 위치와 개인정보

- 설정: `%LocalAppData%\SimpleCapGIF\settings.json`
- 녹화 임시 파일: `%LocalAppData%\SimpleCapGIF\Temp\{session-id}`
- 저장 위치가 없으면 최초 폴더 선택 창은 `%USERPROFILE%\Pictures\SimpleCapGIF`에서 시작합니다.
- 네트워크 요청, 계정, 원격 분석, 업로드, 자동 업데이트 기능이 없습니다.
- 화면 픽셀이나 사용자 파일 내용은 로그에 기록하지 않습니다.

## 제한 사항

- 녹화 영역은 한 모니터 안에 완전히 들어가야 합니다.
- 오디오, 웹캠, 창 추적, 편집, MP4 내보내기는 제공하지 않습니다.
- v0.1은 HDR 톤 매핑을 지원하지 않아 HDR 화면의 색이나 밝기가 다를 수 있습니다.
- DRM 등 보호된 콘텐츠는 검게 녹화될 수 있습니다.
- 영상이나 노이즈처럼 모든 프레임이 크게 바뀌는 콘텐츠는 30MB를 넘을 수 있습니다.

## 라이선스와 개발

SimpleCapGIF은 [MIT License](LICENSE)로 배포됩니다. FFmpeg와 다른 포함 구성 요소의 조건은 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 확인하세요.

빌드, 테스트, 패키징, 아키텍처, 번역 기여 방법은 영문 [DEVELOPMENT.md](DEVELOPMENT.md)를 확인하세요.
