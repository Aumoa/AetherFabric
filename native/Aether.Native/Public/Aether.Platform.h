#pragma once

#if !defined(PLATFORM_WINDOWS) || !defined(PLATFORM_LINUX) || !defined(PLATFORM_MACOS)
    #error "Aether platform macros must be provided by Aether.BuildTool."
#elif (PLATFORM_WINDOWS + PLATFORM_LINUX + PLATFORM_MACOS) != 1
    #error "Exactly one Aether platform macro must be enabled."
#endif
