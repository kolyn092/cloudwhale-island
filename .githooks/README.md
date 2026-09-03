# Git hooks

이 저장소에서는 `타입/기능명` 형식의 작업 브랜치에서만 커밋할 수 있다.

```powershell
.\scripts\install-git-hooks.ps1
```

한 번 설치하면 `main`, 분리된 HEAD, 또는 형식이 맞지 않는 브랜치에서의 커밋과 한국어가 없는 커밋 메시지가 차단된다. 새로 복제한 저장소에서는 이 명령을 한 번 실행한다.

허용 예시:

- `feat/offline-rewards`
- `fix/save-recovery`
- `docs/build-guide`

허용 타입은 `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`, `style`, `hotfix`이다.

커밋 메시지와 PR 제목·본문은 모두 한국어로 작성한다.
