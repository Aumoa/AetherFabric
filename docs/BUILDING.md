# Aether 빌드 가이드

Aether는 관리형 프로젝트에는 표준 .NET 빌드를 사용하고, Native C++ 모듈에는 저장소 전용 `Aether.BuildTool`을 사용한다. Visual Studio의 C++ 프로젝트는 빌드 규칙의 원본이 아니라 Build Tool을 호출하는 NMake 프록시다.

## Visual Studio 솔루션 생성

Windows에서 다음 명령을 실행한다.

```bat
GenerateSolution.bat
```

이 명령은 Build Tool을 `Intermediate/BuildTool`에 publish하고, `Intermediate/ProjectFiles/Aether.sln`을 생성한 뒤 Visual Studio에서 연다. CI나 자동화에서 창을 열지 않으려면 다음 옵션을 사용한다.

```bat
GenerateSolution.bat --no-open
```

Linux 또는 macOS에서는 다음 명령으로 같은 솔루션 파일을 생성할 수 있다.

```sh
./GenerateSolution.sh
```

생성된 솔루션에는 `src`, `tests`, `tools` 아래의 모든 `.csproj`와 `native` 아래의 모든 `*.Module.json` 모듈이 포함된다.

## Native 빌드

먼저 Build Tool을 준비한다.

```bat
Setup.bat
```

그다음 현재 호스트 플랫폼용 Native 모듈을 빌드한다.

```bat
dotnet Intermediate\BuildTool\Aether.BuildTool.dll build --root . --target Aether.Native --configuration Debug
```

지원하는 로컬 toolchain은 다음과 같다.

| 호스트 | Toolchain | 출력 |
|---|---|---|
| Windows | MSVC | `.dll` |
| Linux | Clang 또는 GCC | `.so` |
| macOS | Apple Clang | `.dylib` |

Native 출력은 `artifacts/native/<RID>/<Configuration>`에 생성되고 중간 파일은 `artifacts/obj/native`에 생성된다.

Build Tool은 의도적으로 다른 운영체제용 cross-host 컴파일을 수행하지 않는다. Linux 바이너리는 Linux 호스트나 CI runner에서, macOS 바이너리는 Xcode command line tools가 설치된 macOS 호스트나 CI runner에서 만든다. 동일한 manifest와 Build Tool 명령을 사용하므로 플랫폼별 빌드 의미는 일관되게 유지된다.

## Native 모듈 선언

Native 모듈은 `*.Module.json`으로 선언한다.

```json
{
  "name": "Aether.Native",
  "kind": "sharedLibrary",
  "languageStandard": "c++20",
  "publicIncludeDirectories": ["Public"],
  "privateIncludeDirectories": ["Private"],
  "sourceDirectories": ["Private"],
  "definitions": ["AETHER_NATIVE_BUILD=1"],
  "dependencies": []
}
```

초기 단계에서는 선언형 manifest를 사용한다. 복잡한 조건부 빌드 규칙이 실제로 필요해지기 전까지 실행 가능한 C# 규칙 파일을 도입하지 않는다.

## 진단과 정리

로컬 compiler와 프로젝트 검색 결과를 확인한다.

```bat
dotnet Intermediate\BuildTool\Aether.BuildTool.dll doctor --root .
```

특정 Native 타깃의 산출물을 정리한다.

```bat
dotnet Intermediate\BuildTool\Aether.BuildTool.dll clean --root . --target Aether.Native --configuration Debug
```

`Aether.sln`, `.vcxproj`, `.filters` 같은 생성 파일은 직접 편집하지 않는다. manifest나 Build Tool을 변경한 후 솔루션을 다시 생성한다.
