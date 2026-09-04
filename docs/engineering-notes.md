# 구현 메모

Unity 명령줄 EditMode 검사에 quit 옵션을 함께 주면 검사가 끝나기 전에 종료될 수 있다. 검사 명령에는 quit를 넣지 않고 결과 XML의 통과 수를 확인한다.

PowerShell에서 Unity Editor를 시작하면 셸 반환이 검사·빌드 종료보다 빠를 수 있다. 종료 코드가 필요한 자동 확인은 `Start-Process -Wait -PassThru`로 프로세스를 기다리고, EditMode는 결과 XML, Web 빌드는 로그의 성공 문구와 `Builds/Web/index.html`을 함께 확인한다.

Web 생산 빌드는 Web 대상 모듈이 설치된 Editor에서 CloudWhale.Editor.BuildWeb.Build를 실행한다. 로그의 Build Finished, Result: Success.와 Builds/Web/index.html 생성으로 함께 확인한다.

열린 게임의 생산은 시작 시 자동 생성되는 런타임 동작이 생산 주기마다 상태 계층을 호출해야 한다. 화면에 보이는 집 비용은 상태 계층이 실제로 사용하는 비용과 같아야 한다.

개발 전용 생산 안내는 다음 주기까지 남은 시간을 읽기만 하며 자원이나 저장 상태를 바꾸지 않는다. Unity Editor와 Development Build에만 표시하고 Release 빌드에는 포함하지 않는다.

압축된 WebGL 산출물을 로컬에서 확인할 때 정적 서버는 `.gz` 파일에 `Content-Encoding: gzip` 응답 헤더를 보내야 한다. 이 헤더가 없으면 Unity loader가 압축 파일을 해석하지 못한다.
