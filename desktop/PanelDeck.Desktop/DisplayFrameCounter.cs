namespace PanelDeck;

internal readonly record struct DisplayFrameStats(float? Fps, float? FrameTimeMs, float? Low1Percent);

// Display timestamps, including delayed generated frames. No extra event providers.
internal sealed class DisplayFrameCounter(long frequency)
{
    private const int Capacity = 32768;
    private sealed class Window {
        internal readonly long[] Times = new long[Capacity];
        internal int Start, Count;
        internal long Last => Count == 0 ? 0 : At(Count - 1);
        internal long At(int i) => Times[(Start + i) % Capacity];
        internal void Put(int i, long time) => Times[(Start + i) % Capacity] = time;
        internal void Drop() { Start = (Start + 1) % Capacity; Count--; }
        internal int LowerBound(long time) {
            int lo = 0, hi = Count;
            while (lo < hi) { int mid = lo + (hi - lo) / 2; if (At(mid) < time) lo = mid + 1; else hi = mid; }
            return lo;
        }
    }
    private readonly Dictionary<ulong, Window> chains = new();
    private readonly long[] durations = new long[Capacity];
    private Window? lowWindow;
    private long lowAt;
    private float? cachedLow;
    internal void Reset() { chains.Clear(); lowWindow = null; cachedLow = null; lowAt = 0; }
    internal void Add(ulong chain, long time)
    {
        if (time <= 0) return;
        if (!chains.TryGetValue(chain, out var w)) {
            if (chains.Count == 8) chains.Remove(chains.MinBy(x => x.Value.Last).Key);
            chains.Add(chain, w = new());
        }
        // Resumed/dormant chains must warm up again instead of reusing old statistics.
        if (w.Count > 0 && time - w.Last >= frequency * 3) {
            w.Start = w.Count = 0;
            if (lowWindow == w) { lowWindow = null; cachedLow = null; }
        }
        long cutoff = Math.Max(time, w.Last) - frequency * 30;
        if (time < cutoff) return;
        // Keep a boundary sample to verify coverage of the entire 30-second window.
        while (w.Count > 1 && w.At(1) <= cutoff) w.Drop();
        int at = time > w.Last ? w.Count : w.LowerBound(time);
        if (at < w.Count && w.At(at) == time) return;
        if (w.Count == Capacity) { if (at == 0) return; w.Drop(); at--; }
        for (int i = w.Count; i > at; i--) w.Put(i, w.At(i - 1));
        w.Put(at, time); w.Count++;
    }
    internal float? Read(long now) => ReadStats(now).Fps;
    internal DisplayFrameStats ReadStats(long now)
    {
        Window? best = null; int first = 0, count = 0;
        long newest = chains.Count == 0 ? 0 : chains.Max(x => x.Value.Last);
        foreach (var w in chains.Values) {
            if (w.Count < 2 || now < w.Last || now - w.Last >= frequency * 3 || newest - w.Last > frequency / 2) continue;
            int start = w.LowerBound(w.Last - frequency * 2), length = w.Count - start;
            if (length < 2 || w.Last - w.At(start) < frequency / 2) continue;
            if (best == null || length > count) { best = w; first = start; count = length; }
        }
        if (best == null) { lowWindow = null; cachedLow = null; return default; }
        double seconds = (best.Last - best.At(first)) / (double)frequency;
        float fps = (float)((count - 1) / seconds);
        // Sort at most once per second, reusing storage rather than allocating per frame.
        if (best != lowWindow || now - lowAt >= frequency) {
            lowWindow = best; lowAt = now; cachedLow = null;
            long cutoff = best.Last - frequency * 30;
            if (best.At(0) <= cutoff) {
                int start = best.LowerBound(cutoff), n = best.Count - start - 1;
                if (n >= 100) {
                    for (int i = 0; i < n; i++) durations[i] = best.At(start + i + 1) - best.At(start + i);
                    Array.Sort(durations, 0, n);
                    int slowCount = (n + 99) / 100;
                    double total = 0;
                    for (int i = n - slowCount; i < n; i++) total += durations[i];
                    cachedLow = (float)(slowCount * (double)frequency / total);
                }
            }
        }
        return new(fps, (float)(seconds * 1000 / (count - 1)), cachedLow);
    }
}
