# ADR-0001: Native Build System and Visual Studio Integration

- Status: Accepted
- Date: 2026-09-20

## Context

Aether centers its public platform on C# and .NET, but measured performance bottlenecks may justify C++ native acceleration on specific execution paths. The development environment should prioritize Visual Studio while producing consistent Windows, Linux, and macOS server binaries.

Using conventional Visual Studio C++ projects as the source of truth would couple the build definition to MSBuild and MSVC, creating a risk that Linux and macOS rules evolve independently. Introducing a complete general-purpose build system at this early stage would add complexity beyond the current requirements.

## Decision

- The repository-specific .NET program `Aether.BuildTool` is the single source of truth for native builds.
- Native modules are initially declared through `*.Module.json` files.
- The Build Tool is responsible for module discovery, dependency ordering, output paths, compiler invocation, and Visual Studio project generation.
- Windows uses MSVC, Linux uses Clang or GCC, and macOS uses Apple Clang.
- The Build Tool defines `PLATFORM_WINDOWS`, `PLATFORM_LINUX`, and `PLATFORM_MACOS` as mutually exclusive `0` or `1` values for every native module, avoiding direct use of compiler-specific platform macros.
- Each operating system's binaries are built on a local development environment or CI runner for that operating system.
- Generated Visual Studio C++ projects are NMake projects that delegate the actual build to `Aether.BuildTool`.
- Standard C# projects continue to use their existing `.csproj` files and the .NET SDK build system.
- Generated files and build outputs are isolated under `Intermediate` and `artifacts`.

## Alternatives

### Use Visual Studio C++ Projects as the Source of Truth

This option provides a straightforward Windows development experience but requires separate build rules for other platforms, so it was not selected.

### Use CMake as the Only Native Build System

CMake offers a mature ecosystem and broad IDE support. A repository-specific orchestration layer was selected so that Aether can coordinate managed projects, future code generation, and packaging through one tool. CMake can still be used internally for specific external dependencies if a concrete requirement emerges.

### Use Executable C# Module Rules

Executable rules offer greater expressiveness but introduce complexity in rule compilation, caching, security, and debugging. This option is deferred until real use cases cannot be represented by declarative manifests.

## Consequences

- Visual Studio remains the primary environment for code navigation and debugging.
- The CLI, Visual Studio, and CI use the same native build path.
- macOS builds require a macOS host, and Linux builds require a Linux host.
- Build Tool reliability and tests directly affect repository build reliability.
- The current implementation is a minimal vertical slice. Dependency caching, parallel scheduling, and external package integration will be added only when concrete requirements justify them.
