// In-process, realtime-only PresentMon bridge. No service, overlay, CSV or injection.
#include <algorithm>
#include <atomic>
#include <thread>
#include <memory>
#include "../third_party/PresentMon/PresentData/PresentMonTraceConsumer.hpp"
#include "../third_party/PresentMon/PresentData/PresentMonTraceSession.hpp"

namespace pmon::util::hash {
size_t HashCombine(size_t a, size_t b) noexcept {
    return a ^ (b + 0x517cc1b727220a95ull + (a << 6) + (a >> 2));
}
}
// ABI is explicitly fixed-width. All QPC values use the system performance clock.
struct DisplayFrame { uint64_t chain, time; };
struct ReadInfo {
    uint32_t count, eventsLost, buffersLost, overflows, lostPresents, discarded, failed, reserved;
};
struct Capture {
    PMTraceConsumer consumer{8192};
    PMTraceSession session;
    std::thread thread;
    std::atomic<bool> failed{false};
    uint32_t pid;
    uint64_t hwnd;
    std::vector<std::shared_ptr<PresentEvent>> presents;
    std::vector<ProcessEvent> processes;
    explicit Capture(uint32_t id, uint64_t window) : pid(id), hwnd(window) {
        consumer.mFilteredProcessIds = true;
        consumer.AddTrackedProcessForFiltering(pid);
        consumer.mTrackDisplay = true;
        consumer.mTrackFrameType = true;
        consumer.mTrackGPU = consumer.mTrackGPUVideo = consumer.mTrackInput = false;
        consumer.mTrackPcLatency = consumer.mTrackAppTiming = consumer.mTrackPMMeasurements = false;
        session.mPMConsumer = &consumer;
        LARGE_INTEGER frequency; QueryPerformanceFrequency(&frequency);
        consumer.mDeferralTimeLimit = frequency.QuadPart * 2;
        presents.reserve(8192);
    }
    ~Capture() {
        session.Stop();
        if (thread.joinable()) thread.join();
    }
};
#define EXPORT extern "C" __declspec(dllexport)
EXPORT uint32_t PdAbiVersion() noexcept { return 1; }
EXPORT uint32_t PdStart(uint32_t pid, uint64_t hwnd, Capture** output) noexcept {
    if (!output || !pid) return ERROR_INVALID_PARAMETER;
    *output = nullptr;
    try {
        auto c = std::make_unique<Capture>(pid, hwnd);
        auto name = L"PanelDeck.Display." + std::to_wstring(GetCurrentProcessId());
        auto error = c->session.Start(nullptr, name.c_str());
        if (error) return error;
        c->thread = std::thread([p = c.get()] {
            auto handle = p->session.mTraceHandle;
            ProcessTrace(&handle, 1, nullptr, nullptr);
            p->failed.store(true);
        });
        *output = c.release();
        return ERROR_SUCCESS;
    } catch (...) { return ERROR_GEN_FAILURE; }
}
EXPORT uint32_t PdRead(Capture* c, DisplayFrame* frames, uint32_t capacity, ReadInfo* info) noexcept {
    if (!c || !frames || !info || !capacity) return ERROR_INVALID_PARAMETER;
    *info = {};
    try {
        EtwStatus status{};
        if (!c->session.QueryEtwStatus(&status) || c->failed.load() || c->session.mDecodeFailed.load()) info->failed = 1;
        info->eventsLost = status.mEtwEventsLost;
        info->buffersLost = status.mEtwBuffersLost;
        info->overflows = status.mNumOverflowedPresents;
        c->consumer.DequeueProcessEvents(c->processes);
        c->processes.clear();
        c->consumer.DequeuePresentEvents(c->presents);
        for (auto const& p : c->presents) {
            if (p->ProcessId != c->pid || (p->Hwnd && c->hwnd && p->Hwnd != c->hwnd)) continue;
            if (p->IsLost) { info->lostPresents++; continue; }
            if (p->PresentFailed || p->FinalState != PresentResult::Presented) {
                if (p->FinalState == PresentResult::Discarded) info->discarded++;
                continue;
            }
            // Every distinct on-screen frame counts, including generated frames. Repeats do not.
            for (auto const& display : p->Displayed) {
                if (!display.second || display.first == FrameType::Repeated) continue;
                if (info->count == capacity) { info->failed = 1; break; }
                frames[info->count++] = {p->SwapChainAddress ? p->SwapChainAddress : p->Hwnd, display.second};
            }
        }
        c->presents.clear();
        // Completion order is not display order (particularly with frame generation).
        std::sort(frames, frames + info->count, [](auto const& a, auto const& b) { return a.time < b.time; });
        return ERROR_SUCCESS;
    } catch (...) { info->failed = 1; return ERROR_GEN_FAILURE; }
}
EXPORT void PdStop(Capture* capture) noexcept { delete capture; }
