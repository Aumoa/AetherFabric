#include "Aether.Native.h"

#include <bit>
#include <expected>

#if !defined(__cpp_if_consteval) || __cpp_if_consteval < 202106L
    #error "The C++23 smoke test requires if consteval."
#endif
#if !defined(__cpp_lib_expected) || __cpp_lib_expected < 202202L
    #error "The C++23 smoke test requires std::expected."
#endif
#if !defined(__cpp_lib_byteswap) || __cpp_lib_byteswap < 202110L
    #error "The C++23 smoke test requires std::byteswap."
#endif

constexpr int evaluation_path()
{
    if consteval
    {
        return 23;
    }
    else
    {
        return 1;
    }
}

static_assert(evaluation_path() == 23);
static_assert(std::byteswap(std::uint32_t{0x01020304}) == 0x04030201);

int main()
{
    const std::expected<std::uint32_t, int> version{aether_native_abi_version()};
    const std::expected<std::uint32_t, int> failure{std::unexpected(7)};
    if (!version || *version != 1 || failure || failure.error() != 7 || evaluation_path() != 1)
    {
        return 1;
    }

    try
    {
        (void)failure.value();
        return 1;
    }
    catch (const std::bad_expected_access<int>& error)
    {
        return error.error() == 7 ? 0 : 1;
    }
}
