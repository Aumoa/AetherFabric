#pragma once

#include <cstdint>

#if defined(_WIN32)
    #if defined(AETHER_NATIVE_BUILD)
        #define AETHER_NATIVE_API __declspec(dllexport)
    #else
        #define AETHER_NATIVE_API __declspec(dllimport)
    #endif
#else
    #define AETHER_NATIVE_API __attribute__((visibility("default")))
#endif

extern "C"
{
    AETHER_NATIVE_API std::uint32_t aether_native_abi_version() noexcept;
}
