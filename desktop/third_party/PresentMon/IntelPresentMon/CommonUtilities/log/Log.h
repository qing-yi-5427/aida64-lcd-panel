// PanelDeck host adapter. PresentMon event analysis is unchanged; no logging service.
#pragma once
#include <atomic>
#include <string>
namespace pmon::util::log {
enum class Level { None, Debug };
struct GlobalPolicy {
    static GlobalPolicy& Get() { static GlobalPolicy p; return p; }
    Level GetLogLevel() const { return Level::None; }
};
struct Sink {
    operator bool() const { return false; }
    template<class... T> Sink& note(T&&...) { return *this; }
    template<class... T> Sink& hr(T&&...) { return *this; }
    template<class... T> Sink& watch(T&&...) { return *this; }
    template<class... T> Sink& first(T&&...) { return *this; }
};
}
// Compile out diagnostic formatting/stack capture; ETW losses are exposed by the bridge.
#define pmlog_(...) if (true) {} else ::pmon::util::log::Sink{}
#define pmlog_warn(...) ::pmon::util::log::Sink{}.note(__VA_ARGS__)
#define pmlog_error(...) if (true) {} else ::pmon::util::log::Sink{}
#define pmlog_info(...) if (true) {} else ::pmon::util::log::Sink{}
#define pmwatch(expr) watch(#expr, (expr))

#define pmlog_dbg(...) if (true) {} else ::pmon::util::log::Sink{}
