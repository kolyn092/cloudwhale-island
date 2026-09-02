# 실행과 배포

Unity 6000.3.23f1과 Web Build Support가 설치된 Windows 환경에서 프로젝트를 연다. Hub와 Build Profiles에서는 대상 이름이 Web으로 보일 수 있다.

정확한 자동 검사와 생산 빌드 명령은 docs/implementation/verification.md에 있다. Web 출력은 Builds/Web/index.html이다.

Web 결과는 file 주소로 직접 열지 않는다. Builds/Web을 정적 Web 서버로 제공하고 새 게임, 자원 생산, 집 기초, 새로고침 뒤 복구를 직접 확인한다.

공개 대상은 GitHub Pages다. 실제 공개 URL 배포와 확인은 아직 범위 밖이다.
