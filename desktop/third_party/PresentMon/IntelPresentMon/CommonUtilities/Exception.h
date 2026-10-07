// PanelDeck host adapter: standard C++ exceptions, without API/service or stack tracing.
#pragma once
#include <stdexcept>
#include <utility>
#include <string>
namespace pmon::util {
using Exception = std::runtime_error;
template<class E = Exception, class... T> E Except(T&&... args) { return E(std::forward<T>(args)...); }
inline std::string ReportException() { return "PresentMon event decode failure"; }
}
#define PM_DEFINE_EX(name) class name : public std::runtime_error { public: using std::runtime_error::runtime_error; }
