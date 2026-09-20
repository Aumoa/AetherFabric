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
  "languageStandard": "c++20",
  "publicIncludeDirectories": ["Public"],
  "privateIncludeDirectories": ["Private"],
  "sourceDirectories": ["Private"],
  "definitions": ["AETHER_NATIVE_BUILD=1"],
  "dependencies": []
}
```

The initial build system uses declarative manifests. Do not introduce executable C# rule files until concrete build requirements can no longer be expressed clearly through the manifest.

All Aether native modules require C++20 or later. The Build Tool passes the appropriate C++20 option to each compiler, and `Aether.Platform.h` validates the requirement at compile time through `__cplusplus`.

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
