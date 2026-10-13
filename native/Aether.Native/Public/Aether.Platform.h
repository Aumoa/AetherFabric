#pragma once

// Some C++23 toolchains still report a provisional post-C++20 value.
#if !defined(__cplusplus) || __cplusplus <= 202002L
    #error "Aether Native requires C++23 mode or later."
#endif

#if !defined(PLATFORM_WINDOWS) || !defined(PLATFORM_LINUX) || !defined(PLATFORM_MACOS)
    #error "Aether platform macros must be provided by Aether.BuildTool."
#elif (PLATFORM_WINDOWS + PLATFORM_LINUX + PLATFORM_MACOS) != 1
    #error "Exactly one Aether platform macro must be enabled."
#endif
