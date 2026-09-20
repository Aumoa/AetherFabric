#pragma once

#include "Aether.Platform.h"

#include <cstdint>

#if PLATFORM_WINDOWS
    #if defined(AETHER_NATIVE_BUILD)
        #define AETHER_NATIVE_API __declspec(dllexport)
    #else
        #define AETHER_NATIVE_API __declspec(dllimport)
    #endif
#elif PLATFORM_LINUX || PLATFORM_MACOS
    #define AETHER_NATIVE_API __attribute__((visibility("default")))
#else
    #error "Unsupported Aether platform."
#endif

extern "C"
{
    AETHER_NATIVE_API std::uint32_t aether_native_abi_version() noexcept;
}
