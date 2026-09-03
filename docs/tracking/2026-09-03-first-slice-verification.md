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

## 사용자 직접 확인

- 사용자가 직접 확인한 결과: 열린 게임에서 60초마다 네 자원이 각각 1개씩 증가했다.
- 사용자가 직접 확인한 결과: 네 자원이 각각 5개일 때 집 기초 건설이 가능했다.
- 전달받은 확인 내용에는 실행 환경, 화면 증거, 건설 뒤 자원 차감·집 표현 변화, 저장 복구 결과가 포함되지 않았다. 따라서 이 두 항목만 사용자 직접 확인으로 기록하며, 나머지 직접 확인 항목의 대체 통과로 해석하지 않는다.

## 임시 Chrome 프로필 보완 확인

- 프로필 경로 `C:\Users\kosuk\AppData\Local\Temp\cloudwhale-t1-browser-profile-20260903`를 새로 만들고, 아래 명령으로 기존 사용자 Chrome과 분리해 `http://127.0.0.1:41875/`를 열었다.

```powershell
& 'C:\Program Files\Google\Chrome\Application\chrome.exe' "--user-data-dir=C:\Users\kosuk\AppData\Local\Temp\cloudwhale-t1-browser-profile-20260903" '--no-first-run' '--no-default-browser-check' '--new-window' 'http://127.0.0.1:41875/'
```

- 실행 직후 새 Chrome 프로세스의 명령줄에 위 `--user-data-dir` 경로가 포함된 것을 확인했고, 창 제목 `Unity Web Player | CloudWhale Island - Chrome`도 확인했다.
- 해당 창의 화면 캡처를 수집하려 했지만 Windows UI 자동화가 현재 Chrome URL을 정책상 충분히 판별하지 못해 즉시 중단됐다. 따라서 임시 프로필 Chrome에서의 화면 증거는 수집하지 못했으며, 이전 절의 화면 증거로 대체 통과라고 쓰지 않는다.
- 임시 프로필 Chrome 프로세스(50144, 44092)를 종료한 뒤 프로필 디렉터리 전체를 삭제했다. 삭제 뒤 경로 존재 확인 결과는 `false`였다. 로컬 정적 서버도 종료했다.

## 분리 프로필 화면 증거 최종 보완 시도

- 두 번째이자 마지막 보완 시도에서는 새 경로 `C:\Users\kosuk\AppData\Local\Temp\cloudwhale-t1-browser-evidence-20260903-retry2`를 만들고, Chrome을 `--headless=new`, 해당 `--user-data-dir`, `--remote-debugging-port=9223`, `--window-size=1440,1000`으로 실행했다. 기존 사용자 Chrome 프로필은 열거나 수정하지 않았다.
- 실행 중인 Chrome 프로세스 명령줄에서 위 `--user-data-dir`가 적용된 것을 확인했고, DevTools 대상 목록에서 `http://127.0.0.1:41876/`와 제목 `Unity Web Player | CloudWhale Island`를 확인했다.
- 이 환경에서 지원되는 Browser 제어 경로는 앱 내 브라우저 또는 기존 브라우저 확장 연결이며, 임의의 `--user-data-dir`로 시작한 분리 프로필을 그 제어 경로에 연결할 수 없었다. 기존 Chrome 확장 연결은 사용자 프로필을 열 수 있으므로 사용하지 않았다.
- 분리 프로필에 한정해 DevTools Protocol로 `Runtime.evaluate`와 `Page.captureScreenshot`을 요청했지만 페이지 대상이 응답하지 않아 각각 제한 시간 안에 완료되지 않았고, PNG 파일도 생성되지 않았다. 따라서 초기 화면을 포함한 명세 흐름의 직접 화면 증거를 새 분리 프로필에서 확보하지 못했다.
- 이 최종 보완 시도에 사용한 `npx --yes http-server . -p 41876 -g -c-1 --silent` 응답도 `Build/Web.framework.js.gz`에 `Content-Encoding: gzip`을 반환하지 않았다. 앞선 직접 Web 확인은 gzip 헤더를 명시한 별도 정적 서버에서 성공했지만, 이번 시도의 실행 제목만으로 게임 화면이 정상 렌더링됐다고 판정하지 않는다.
- 이미 확보한 근거는 EditMode 22/22, Web 생산 빌드 성공, gzip 헤더 정적 서버에서의 앞선 Web 상호작용 관찰이다. 이 근거들은 새 분리 프로필의 화면 증거나 Unity Editor Play 모드 직접 확인을 대체하지 않는다. 그러므로 명세 전체의 직접 화면 검증은 미완료다.
- 분리 프로필 Chrome 프로세스 8개와 포트 41876의 정적 서버 프로세스를 종료했다. 삭제 대상을 정확한 절대 경로로 재확인한 뒤 프로필 디렉터리 전체를 삭제했고, `Test-Path` 결과는 `False`였다. 포트 41876과 9223도 더 이상 수신하지 않는다.

## 정적 서버 관찰

- 기본 `python -m http.server`는 압축된 `.gz`에 `Content-Encoding: gzip`을 보내지 않아 Unity loader가 `Unable to parse Build/Web.framework.js.gz`로 실패했다.
- 압축 헤더를 추가한 로컬 정적 서버에서는 게임이 정상 실행됐다. 이는 게임 코드 결함으로 확정하지 않았으며, 배포 정적 서버에는 gzip 응답 헤더가 필요하다는 운영 조건이다.
