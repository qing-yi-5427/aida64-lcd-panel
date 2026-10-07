namespace PanelDeck;

// Bounded, allocation-free per-frame accounting. Present timestamps use the ETW QPC clock.
internal sealed class FrameRateCounter(long frequency)
{
    private readonly object gate = new();
    private readonly Dictionary<(bool D3d9, uint Thread), (ulong Chain, long Time, ushort Stop)> pending = new();
    private readonly Dictionary<(bool D3d9, ulong Chain), Samples> chains = new();
    private sealed class Samples
    {
        internal readonly long[] Times = new long[4096];
        internal int Start, Count;
        internal long Last;
    }
    internal void Reset() { lock (gate) { pending.Clear(); chains.Clear(); } }
    internal void Begin(bool d3d9, uint thread, ulong chain, long time, uint flags, ushort stop)
    {
        lock (gate) {
            var key = (d3d9, thread);
            pending.Remove(key);
            // DXGI_PRESENT_TEST and D3DPRESENT_DONOTFLIP don't submit a new frame.
            if ((flags & (d3d9 ? 4u : 1u)) != 0) return;
            if (pending.Count >= 64) pending.Clear();
            pending[key] = (chain, time, stop);
        }
    }
    internal void End(bool d3d9, uint thread, ushort id, uint result)
    {
        lock (gate) {
            if (!pending.Remove((d3d9, thread), out var present) || present.Stop != id || result != 0) return;
            var key = (d3d9, present.Chain);
            if (!chains.TryGetValue(key, out var s)) {
                if (chains.Count >= 8) {
                    var oldest = chains.MinBy(x => x.Value.Last);
                    chains.Remove(oldest.Key);
                }
                chains[key] = s = new();
            }
            // Drop duplicates and out-of-order deliveries rather than corrupting the average.
            if (s.Count > 0 && present.Time <= s.Last) return;
            s.Last = present.Time;
            while (s.Count > 0 && (s.Count == s.Times.Length || s.Times[s.Start] < s.Last - frequency * 2)) {
                s.Start = (s.Start + 1) % s.Times.Length; s.Count--;
            }
            s.Times[(s.Start + s.Count) % s.Times.Length] = s.Last; s.Count++;
        }
    }
    internal float? Read(long now)
    {
        lock (gate) {
            // Use the busiest swap chain, never sum game + video/overlay swap chains.
            Samples? best = null;
            foreach (var s in chains.Values) {
                if (s.Count < 2 || now < s.Last || now - s.Last >= frequency * 3 || s.Last - s.Times[s.Start] < frequency / 2) continue;
                if (best == null || s.Count > best.Count) best = s;
            }
            return best == null ? null : (float)((best.Count - 1) * (double)frequency / (best.Last - best.Times[best.Start]));
        }
    }
}
