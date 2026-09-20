# ADR-0001: Native 빌드 시스템과 Visual Studio 통합

- 상태: Accepted
- 날짜: 2026-09-20

## 컨텍스트

Aether의 공개 플랫폼은 C#/.NET을 중심으로 하지만, 측정으로 필요성이 확인된 경로는 C++ Native 가속기를 사용할 수 있다. 개발 환경은 Visual Studio를 우선하면서도 Windows, Linux, macOS 서버 바이너리를 일관된 방식으로 빌드해야 한다.

일반적인 Visual Studio C++ 프로젝트를 빌드 정의의 원본으로 사용하면 MSBuild와 MSVC에 결합되고, Linux와 macOS 빌드 규칙이 별도로 발전할 위험이 있다. 반대로 범용 빌드 시스템 전체를 초기에 도입하면 현재 규모에 비해 불필요한 복잡성이 생긴다.

## 결정

- 저장소 전용 .NET 프로그램인 `Aether.BuildTool`이 Native 빌드의 단일 진실 공급원이 된다.
- Native 모듈은 우선 선언형 `*.Module.json`으로 정의한다.
- Build Tool은 모듈 검색, 의존성 순서, 산출물 경로, compiler 실행과 Visual Studio 프로젝트 생성을 담당한다.
- Windows에서는 MSVC, Linux에서는 Clang 또는 GCC, macOS에서는 Apple Clang을 사용한다.
- 각 운영체제의 바이너리는 해당 운영체제의 로컬 개발 환경이나 CI runner에서 빌드한다.
- 생성된 Visual Studio C++ 프로젝트는 NMake 프로젝트이며 실제 빌드는 `Aether.BuildTool`에 위임한다.
- 표준 C# 프로젝트는 기존 `.csproj`와 .NET SDK 빌드를 그대로 사용한다.
- 생성 파일과 빌드 산출물은 `Intermediate`와 `artifacts`에 격리한다.

## 대안

### Visual Studio C++ 프로젝트를 원본으로 사용

Windows 개발 경험은 단순하지만 다른 플랫폼의 규칙을 별도로 유지해야 하므로 선택하지 않았다.

### CMake를 유일한 Native 빌드 시스템으로 사용

성숙한 생태계와 IDE 지원이 장점이다. 다만 Aether의 관리형 프로젝트 조합, 향후 코드 생성과 패키징을 하나의 도구에서 조정하기 위해 저장소 전용 orchestration 계층을 선택했다. 필요해지면 Build Tool 내부의 특정 외부 의존성 빌드에 CMake를 사용할 수 있다.

### 실행 가능한 C# Module 규칙

표현력이 높지만 규칙 컴파일, 캐시, 보안과 디버깅 복잡성이 커서 초기 단계에서는 보류한다. 선언형 manifest로 표현하기 어려운 실제 사례가 생기면 다시 검토한다.

## 결과

- Visual Studio는 코드 탐색과 디버깅 환경으로 유지된다.
- CLI, Visual Studio와 CI가 같은 Native 빌드 경로를 사용한다.
- macOS 빌드에는 macOS 호스트가, Linux 빌드에는 Linux 호스트가 필요하다.
- Build Tool 자체의 안정성과 테스트가 저장소 빌드 신뢰성에 직접 영향을 준다.
- 현재 구현은 최소 수직 기능이며, dependency cache, 병렬 scheduler, 외부 패키지 통합은 실제 필요가 확인될 때 확장한다.
