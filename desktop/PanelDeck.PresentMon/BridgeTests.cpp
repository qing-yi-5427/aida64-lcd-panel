// Synthetic completed-event tests. Never starts ETW or changes the foreground window.
#include "Bridge.cpp"
#include <iostream>
#include <stdexcept>

static void Check(bool ok, const char* message) {
    if (!ok) throw std::runtime_error(message);
    std::cout << "PASS: " << message << '\n';
}
static auto Present(uint32_t pid = 42, uint64_t hwnd = 100) {
    auto p = std::make_shared<PresentEvent>();
    p->ProcessId = pid; p->Hwnd = hwnd; p->SwapChainAddress = 7;
    p->FinalState = PresentResult::Presented;
    p->Displayed = {{FrameType::Application, 300}, {FrameType::Intel_XEFG, 200},
        {FrameType::Unspecified, 100}, {FrameType::Repeated, 400}, {FrameType::Application, 0}};
    return p;
}
static void Queue(Capture& c, std::initializer_list<std::shared_ptr<PresentEvent>> events) {
    c.consumer.mCompletedIndex = 0;
    c.consumer.mReadyCount = c.consumer.mCompletedCount = static_cast<uint32_t>(events.size());
    std::copy(events.begin(), events.end(), c.consumer.mCompletedPresents.begin());
}
int main() {
    try {
        Capture c{42, 100};
        DisplayFrame frames[16]{}; ReadInfo info{};
        auto dropped = Present(); dropped->FinalState = PresentResult::Discarded;
        auto failed = Present(); failed->PresentFailed = true;
        auto lost = Present(); lost->IsLost = true;
        Queue(c, {Present(), Present(43), Present(42, 101), dropped, failed, lost});
        Check(PdRead(&c, frames, 16, &info) == ERROR_SUCCESS, "Bridge reads a completed-event fixture");
        Check(info.count == 3, "Count application and generated entries; exclude repeats, zero, other PID/window, failed and lost frames");
        Check(frames[0].time == 100 && frames[1].time == 200 && frames[2].time == 300 && frames[0].chain == 7,
            "Display entries preserve the swap chain and sort by display time");
        Check(info.discarded == 1 && info.lostPresents == 1, "Discarded and lost frames remain distinct diagnostics");
        Check(info.failed == 1, "An unstarted session cannot claim valid live tracing");
        PdRead(&c, frames, 16, &info);
        Check(info.count == 0, "Dequeued frames are not emitted again");
        auto noWindow = Present(42, 0); noWindow->SwapChainAddress = 8;
        auto noChain = Present(); noChain->SwapChainAddress = 0;
        Queue(c, {noWindow, noChain}); PdRead(&c, frames, 16, &info);
        Check(info.count == 6, "Missing HWND does not reject valid process frames; missing swap chain uses HWND");
        Check(std::count_if(frames, frames + 6, [](auto f) { return f.chain == 100; }) == 3, "HWND fallback keeps chain identity");
        Queue(c, {Present()}); PdRead(&c, frames, 1, &info);
        Check(info.count == 1 && info.failed == 1, "Output capacity is bounded and incomplete batches are invalid");
        Check(PdRead(nullptr, frames, 16, &info) == ERROR_INVALID_PARAMETER, "Null capture is rejected");
        Capture* handle = nullptr;
        Check(PdStart(0, 0, &handle) == ERROR_INVALID_PARAMETER && handle == nullptr, "Invalid target cannot start a trace");
        Check(sizeof(DisplayFrame) == 16 && sizeof(ReadInfo) == 32 && PdAbiVersion() == 1, "Native ABI sizes and version are fixed");
        return 0;
    } catch (std::exception const& e) { std::cerr << e.what() << '\n'; return 1; }
}
