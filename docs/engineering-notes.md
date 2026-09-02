# 구현 메모

Unity 명령줄 EditMode 검사에 quit 옵션을 함께 주면 검사가 끝나기 전에 종료될 수 있다. 검사 명령에는 quit를 넣지 않고 결과 XML의 통과 수를 확인한다.

Web 생산 빌드는 Web 대상 모듈이 설치된 Editor에서 CloudWhale.Editor.BuildWeb.Build를 실행한다. 로그의 Build Finished, Result: Success.와 Builds/Web/index.html 생성으로 함께 확인한다.

열린 게임의 생산은 시작 시 자동 생성되는 런타임 동작이 생산 주기마다 상태 계층을 호출해야 한다. 화면에 보이는 집 비용은 상태 계층이 실제로 사용하는 비용과 같아야 한다.
