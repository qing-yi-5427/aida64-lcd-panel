// PanelDeck only starts realtime sessions. No high-resolution timer or ETL replay pacing.
#pragma once
#include <windows.h>
#include <stdint.h>
namespace pmon::util {
inline int64_t GetCurrentTimestamp() { LARGE_INTEGER v; QueryPerformanceCounter(&v); return v.QuadPart; }
inline double GetTimestampPeriodSeconds() { LARGE_INTEGER v; QueryPerformanceFrequency(&v); return 1.0 / v.QuadPart; }
inline double TimestampDeltaToSeconds(int64_t a, int64_t b, double period) { return (b - a) * period; }
struct PrecisionWaiter {
    explicit PrecisionWaiter(double) {}
    void Wait(double seconds) { if (seconds > 0) Sleep((DWORD)(seconds * 1000)); }
};
}
