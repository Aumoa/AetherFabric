# ADR-0003: C++23 Baseline and Portable Feature Selection

- Status: Accepted
- Date: 2026-10-14

## Context

Aether supports Windows, Linux, and macOS native modules. The user selected C++23 as the baseline while requiring unavailable features to be removed or replaced across supported compilers.

## Decision

Use C++23 mode for native modules and generated Visual Studio IntelliSense settings. The current Windows toolchain uses `/std:c++23preview`; Unix toolchains use `-std=c++23`. Do not use a latest-standard mode that also enables future standards.

Restrict code to language and library features validated on MSVC, GCC, upstream Clang, and Apple Clang. Evaluate compiler and standard-library versions together, including Apple SDK and deployment requirements. Check feature-test macros and compile and run relevant tests when adding features. Prefer a portable replacement when any supported configuration lacks a feature.

Allow explicit Unix compiler selection through `CXX`, without silent fallback. Clean intermediate outputs when switching compilers.

## Alternatives

- Retain C++20: offers broader older-toolchain coverage but does not satisfy the selected baseline.
- Use every C++23 feature: rejected because standard-mode support does not imply complete language and library implementation.
- Use compiler-specific implementations: deferred unless a concrete platform requirement justifies an isolated implementation behind the existing boundary.

## Consequences

Older toolchains are not implicitly supported. MSVC preview mode requires care around C++ ABI compatibility; the exported Aether ABI remains C-compatible. The small native smoke test verifies selected features and library loading, not complete C++23 conformance. Reconsider the MSVC flag when a supported stable C++23 mode is adopted.

The initial validation excluded `std::expected` because Linux Clang 18.1.3 with the installed libstdc++ did not expose it. Subsequent validation on 2026-10-14 upgraded the Linux compiler matrix to GCC 15.2 and Clang 20.1.2. The expanded smoke test passed on these configurations, MSVC 14.44, and Apple Clang 21, so `std::expected` is now part of the allowed shared feature set alongside `if consteval` and `std::byteswap`. The older Clang combination is outside the revised validated baseline. Compiler and library details are recorded in `docs/BUILDING.md`.

Following the user's upgrade request, the local environment adopted stable MSVC 14.51 through a side-by-side Visual Studio Build Tools 2026 installation, installed Clang 20 packages in WSL, and permanently deployed GCC 15.2 under `/opt/aether/gcc-15.2`. Existing compiler installations and the stable Linux standard-library runtime were preserved. Generated NMake projects use the installed Visual Studio default toolset rather than hard-coding `v143`.

The MSVC flag remains `/std:c++23preview`: MSVC 14.51 rejects `/std:c++23`, and the official toolchain status still lists 14.52 as Preview. No macOS operating-system upgrade was performed; no Command Line Tools update was offered by the update inventory.
