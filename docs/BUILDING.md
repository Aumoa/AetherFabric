# Aether Build Guide

Aether uses the standard .NET build system for managed projects and the repository-specific `Aether.BuildTool` for native C++ modules. Visual Studio C++ projects are NMake proxies that invoke the Build Tool; they are not the source of truth for build rules.

## Generate the Visual Studio Solution

Run the following command on Windows:

```bat
GenerateSolution.bat
```

This command publishes the Build Tool to `Intermediate/BuildTool`, generates `Intermediate/ProjectFiles/Aether.sln`, and opens it in Visual Studio. Use the following option in CI or other automation that must not open a window:

```bat
GenerateSolution.bat --no-open
```

Run the equivalent command on Linux or macOS:

```sh
./GenerateSolution.sh
```

The generated solution includes every `.csproj` under `src`, `tests`, and `tools`, along with every `*.Module.json` module under `native`.

## Build Native Modules

Prepare the Build Tool first. On Windows, run:

```bat
Setup.bat
```

On Linux or macOS, run:

```sh
./Setup.sh
```

Then build a native module for the current host platform:

```sh
dotnet Intermediate/BuildTool/Aether.BuildTool.dll build --root . --target Aether.Native --configuration Debug
```

The supported local toolchains are:

| Host | Toolchain | Output |
|---|---|---|
| Windows | MSVC | `.dll` |
| Linux | Clang or GCC | `.so` |
| macOS | Apple Clang | `.dylib` |

Native outputs are written to `artifacts/native/<RID>/<Configuration>`, and intermediate files are written to `artifacts/obj/native`.

The Build Tool intentionally does not perform cross-host compilation. Build Linux binaries on a Linux host or CI runner, and build macOS binaries on a macOS host or CI runner with the Xcode command-line tools installed. Every platform uses the same manifest and Build Tool commands, keeping build semantics consistent.

## Declare Native Modules

Declare each native module in a `*.Module.json` file:

```json
{
  "name": "Aether.Native",
  "kind": "sharedLibrary",
  "languageStandard": "c++23",
  "publicIncludeDirectories": ["Public"],
  "privateIncludeDirectories": ["Private"],
  "sourceDirectories": ["Private"],
  "definitions": ["AETHER_NATIVE_BUILD=1"],
  "dependencies": []
}
```

The initial build system uses declarative manifests. Do not introduce executable C# rule files until concrete build requirements can no longer be expressed clearly through the manifest.

All Aether native modules use C++23. The Build Tool currently accepts only `c++23` manifests and passes `-std=c++23` on Unix and `/std:c++23preview` on MSVC. The MSVC option requires Visual Studio 2022 17.13 or later; preview features can have ABI changes between toolchain releases, so use matching toolchains for components sharing C++ ABI. The public native boundary remains a C ABI.

`Aether.Platform.h` rejects C++20 and older language modes through `__cplusplus`. It accepts provisional post-C++20 values used by C++23 implementations; this is a language-mode check, not proof of complete standard support.

Only use language and standard-library features verified on MSVC, GCC, upstream Clang, and Apple Clang. Check feature-test macros and compile and run the affected code on each supported toolchain. Remove or replace unavailable features. On macOS, include the Xcode/Command Line Tools SDK and deployment target in compatibility decisions.

On Linux and macOS, set `CXX` to a compiler executable name or path (without arguments) to select a toolchain explicitly, for example `CXX=g++` or `CXX=clang++`. An invalid explicit selection fails without falling back. Run `clean` followed by `build` when switching compilers because intermediate paths are shared between compilers.

`tests/native/Cpp23Smoke.cpp` checks a small shared feature set (`if consteval`, `std::byteswap`, and `std::expected`) and calls the native ABI. The expected checks cover successful values, error values, and `bad_expected_access` exceptions. Compile it in C++23 mode with the public include directory, the platform definitions, and the native library, then run the executable. Passing this check does not certify all C++23 features.

Initial validation on 2026-10-14 found `std::expected` unavailable with Ubuntu Clang 18.1.3 and the installed libstdc++ headers. A subsequent isolated compiler upgrade resolved the restriction. The current validated baseline is:

| Host | Compiler | Library/SDK | C++23 mode |
|---|---|---|---|
| Windows | MSVC Build Tools 14.51.36231 (Visual Studio Build Tools 2026 18.10) | Matching MSVC STL | `/std:c++23preview` |
| Linux | GCC 15.2.0 (Ubuntu toolchain PPA package) | libstdc++ 15.2 headers; installed 14.2 runtime | `-std=c++23` |
| Linux | Ubuntu Clang 20.1.2 | Installed libstdc++ 14 headers and 14.2 runtime | `-std=c++23` |
| macOS | Apple Clang 21.0.0 | libc++; macOS 26.5 SDK | `-std=c++23` |

All four configurations passed the native build and the expanded smoke test. `std::expected` is allowed in shared native code against this baseline. Clang 18 is not part of the revised validated baseline. Other compiler/library combinations must pass the same checks before adoption. These are tested versions, not exhaustive claims about the earliest supported versions. Older macOS deployment targets have not been certified by this check.

The Linux upgrades were initially tested under ignored `artifacts/cpp23-upgraded`, then deployed permanently to the local Ubuntu 24.04 WSL environment. Clang 20 is installed through Ubuntu packages. GCC 15.2 compiler and development packages are extracted under `/opt/aether/gcc-15.2`, preserving the installed stable libstdc++ 14.2 runtime instead of accepting the toolchain PPA's GCC 16 development runtime upgrade. The `clang++` and `g++` alternatives select the new compilers; existing GCC installations remain available. `Aether.BuildTool` chooses Clang by default, and `CXX=g++` selects GCC explicitly.

Windows now has stable Visual Studio Build Tools 2026 installed alongside the retained 2022 installation. The Build Tool discovers the newest stable installation through `vswhere`. Generated NMake projects use the Visual Studio default platform toolset rather than forcing `v143`, keeping the editor and build entry point usable with either installation. The new MSVC 14.51 compiler also rejects `/std:c++23`, so `/std:c++23preview` remains required.

Apple Clang was retained because the Mac update inventory did not offer a Command Line Tools update. No macOS operating-system upgrade or reboot was performed. The MSVC flag must be revalidated before switching to a stable C++23 option. See [Microsoft's toolchain status](https://learn.microsoft.com/en-us/cpp/overview/compiler-versions) and [C++23 rollout](https://devblogs.microsoft.com/cppblog/c23-support-in-msvc-build-tools-14-51/) for the distinction between a stable compiler release and a stable C++23 mode.

## Platform Macros

The Build Tool defines all of the following macros as either `0` or `1` for every native module:

| Target platform | `PLATFORM_WINDOWS` | `PLATFORM_LINUX` | `PLATFORM_MACOS` |
|---|---:|---:|---:|
| Windows | 1 | 0 | 0 |
| Linux | 0 | 1 | 0 |
| macOS | 0 | 0 | 1 |

Use this contract instead of compiler-specific macros in native code:

```cpp
#if PLATFORM_WINDOWS
    // Windows-specific implementation
#elif PLATFORM_LINUX
    // Linux-specific implementation
#elif PLATFORM_MACOS
    // macOS-specific implementation
#endif
```

These names are built-in Build Tool definitions and cannot be overridden through the `definitions` field in `*.Module.json`. Public Aether native headers include `Aether.Platform.h`, which verifies that all three values are defined and exactly one platform is active.

## Diagnose and Clean

Inspect the local compiler and discovered projects:

```sh
dotnet Intermediate/BuildTool/Aether.BuildTool.dll doctor --root .
```

Clean the outputs for a specific native target:

```sh
dotnet Intermediate/BuildTool/Aether.BuildTool.dll clean --root . --target Aether.Native --configuration Debug
```

Do not edit generated files such as `Aether.sln`, `.vcxproj`, or `.filters` files. Regenerate the solution after changing a manifest or the Build Tool.
