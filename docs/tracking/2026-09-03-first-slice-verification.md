# 첫 구현 단면 검증 기록 (2026-09-03)

## 범위와 경계

- 검증 대상은 `docs/t1-verify-first-slice` 브랜치의 Web 생산 빌드와 실행 화면이다.
- 기대값은 열린 게임에서 60초마다 유목·구름솜·이슬·별가루가 각각 1개씩 증가하고, 집 기초는 각각 5개를 한 번만 소비하는 것이다.
- 기존 사용자 Chrome 프로필과 저장소는 읽기·수정·삭제하지 않았다. 초기 확인은 `http://127.0.0.1:41874` 전용 원점에서 수행했다. 이후 보완 확인에는 명시적 임시 Chrome 프로필을 사용했다.

## 자동 검사

실행 명령:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'H:\personal\SpartaCamp\cloudwhale-island\.dryforge\worktrees\T1' -runTests -testPlatform EditMode -testResults 'H:\personal\SpartaCamp\cloudwhale-island\.dryforge\worktrees\T1\TestResults\EditMode.xml' -logFile 'H:\personal\SpartaCamp\cloudwhale-island\.dryforge\worktrees\T1\TestResults\EditMode.log'
```

- 시작 셸 종료 코드: `0`.
- Unity 로그: `Test run completed. Exiting with code 0 (Ok).`
- XML 결과: 총 22개, 통과 22개, 실패 0개, 건너뜀 0개.
- 포함된 자동 검사 근거: 부족 자원/반복 건설 거부 시 상태 불변, 충분한 자원의 1회 비용 차감, 열린 게임 생산·저장, 완결 주기만의 오프라인 생산, 저장 읽기·쓰기 실패 안내, 음수 자원·잘못된 단계 저장 복구를 확인한다.

## Web 생산 빌드

실행 명령:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'H:\personal\SpartaCamp\cloudwhale-island\.dryforge\worktrees\T1' -executeMethod CloudWhale.Editor.BuildWeb.Build -quit -logFile 'H:\personal\SpartaCamp\cloudwhale-island\.dryforge\worktrees\T1\Logs\unity-web-build.log'
```

- 시작 셸 종료 코드: `0`.
- Unity 로그: `Build Finished, Result: Success.` 및 `Exiting batchmode successfully now!`.
- 산출물: `Builds/Web/index.html` 존재 확인.

## 직접 Web 확인

- `file://`는 사용하지 않았다. `Builds/Web`을 `127.0.0.1:41874` 정적 서버로 제공했고, `.gz` 파일에 `Content-Encoding: gzip`을 설정했다. HTTP HEAD 응답에서 `Content-Encoding: gzip`을 확인했다.
- 첫 화면 화면 증거: 고래 섬, 미건설 집 자리, 네 자원 0, `Build foundation` 행동, `Progress saved in this browser.` 안내가 보였다.
- 열린 상태에서 약 65초 뒤 실제 관찰값은 네 자원 모두 `1`이었다. 기대값(60초당 각각 1)과 일치한다.
- 약 5분 뒤 실제 관찰값은 네 자원 모두 `5`였다. 건설 뒤 실제 관찰값은 네 자원 모두 `0`, 집은 `Foundation`이었고 집 표현이 기초 모양으로 바뀌었다. 기대값(각각 5 소비, 한 번 건설)과 일치한다.
- 새로고침 뒤 실제 관찰값은 네 자원 `0`, 집 `Foundation`으로 복구됐다.
- 탭을 약 65초 닫았다가 다시 열었을 때 실제 관찰값은 네 자원 모두 `1`, 집 `Foundation`이었다. 완전히 지난 60초 생산 주기 1회만 반영된 기대값과 일치한다.
- 부족 자원 및 이미 기초 상태의 거부 사유는 자동 검사로 검증됐지만, 실행 화면에서는 이미 기초 뒤 행동 버튼이 사라져 동일 UI 행동을 직접 재시도할 수 없었다.

## 직접 Unity Editor 확인

- Unity Editor를 비배치 모드로 열어 대상 프로젝트 창(`T1 - Untitled - Web - Unity 6.3 LTS (6000.3.23f1)`)이 열리는 것까지 확인했다.
- 이 환경에서는 Editor의 Play 모드 상호작용과 화면 캡처를 신뢰성 있게 수집할 수 없었다. 따라서 Editor 직접 게임 흐름은 미확인으로 남기고, 위 Web 직접 확인 및 EditMode 자동 검사로 대체 통과라고 쓰지 않는다.

## 임시 Chrome 프로필 보완 확인

- 프로필 경로 `C:\Users\kosuk\AppData\Local\Temp\cloudwhale-t1-browser-profile-20260903`를 새로 만들고, 아래 명령으로 기존 사용자 Chrome과 분리해 `http://127.0.0.1:41875/`를 열었다.

```powershell
& 'C:\Program Files\Google\Chrome\Application\chrome.exe' "--user-data-dir=C:\Users\kosuk\AppData\Local\Temp\cloudwhale-t1-browser-profile-20260903" '--no-first-run' '--no-default-browser-check' '--new-window' 'http://127.0.0.1:41875/'
```

- 실행 직후 새 Chrome 프로세스의 명령줄에 위 `--user-data-dir` 경로가 포함된 것을 확인했고, 창 제목 `Unity Web Player | CloudWhale Island - Chrome`도 확인했다.
- 해당 창의 화면 캡처를 수집하려 했지만 Windows UI 자동화가 현재 Chrome URL을 정책상 충분히 판별하지 못해 즉시 중단됐다. 따라서 임시 프로필 Chrome에서의 화면 증거는 수집하지 못했으며, 이전 절의 화면 증거로 대체 통과라고 쓰지 않는다.
- 임시 프로필 Chrome 프로세스(50144, 44092)를 종료한 뒤 프로필 디렉터리 전체를 삭제했다. 삭제 뒤 경로 존재 확인 결과는 `false`였다. 로컬 정적 서버도 종료했다.

## 정적 서버 관찰

- 기본 `python -m http.server`는 압축된 `.gz`에 `Content-Encoding: gzip`을 보내지 않아 Unity loader가 `Unable to parse Build/Web.framework.js.gz`로 실패했다.
- 압축 헤더를 추가한 로컬 정적 서버에서는 게임이 정상 실행됐다. 이는 게임 코드 결함으로 확정하지 않았으며, 배포 정적 서버에는 gzip 응답 헤더가 필요하다는 운영 조건이다.
