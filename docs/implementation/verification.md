# 로컬 검증 방법

이 프로젝트는 Unity `6000.3.23f1`과 설치된 `Web Build Support` 모듈을 기준으로 확인한다.

## 자동 검사

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'H:\personal\SpartaCamp\cloudwhale-island' `
  -runTests -testPlatform EditMode `
  -testResults 'H:\personal\SpartaCamp\cloudwhale-island\TestResults\EditMode.xml' `
  -logFile 'H:\personal\SpartaCamp\cloudwhale-island\TestResults\EditMode.log'
```

## Web 생산 빌드

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'H:\personal\SpartaCamp\cloudwhale-island' `
  -executeMethod CloudWhale.Editor.BuildWeb.Build `
  -quit `
  -logFile 'H:\personal\SpartaCamp\cloudwhale-island\Logs\unity-web-build.log'
```

성공하면 `Builds/Web/index.html`이 생성된다. 직접 브라우저에서 확인할 때는 [Unity Editor 확인 목록](unity-editor-checklist.md)의 Web 빌드 확인 절차를 따른다.
