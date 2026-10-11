namespace PlayVoice.Audio;

/// <summary>Session-only playback order. Holds at most 20 past, one current and 20 future entries.</summary>
internal sealed class PlaybackQueue<T> where T : class
{
    internal const int NeighborLimit = 20;
    private readonly List<Entry> past = new(NeighborLimit);
    private readonly List<Entry> future = new(NeighborLimit);
    private IReadOnlyList<T> tracks = Array.Empty<T>();
    private PlaybackMode mode;
    private Entry current;
    private int sourceCount;
    private sealed record Entry(T Track, ulong Seed, int Remaining);

    public T Current => current?.Track;
    internal int HistoryCount => past.Count;
    internal int UpcomingCount => future.Count;

    public void Synchronize(IReadOnlyList<T> source, PlaybackMode playbackMode, bool changed = false)
    {
        if (!changed && ReferenceEquals(tracks, source) && sourceCount == source.Count && mode == playbackMode) return;
        tracks = source;
        sourceCount = source.Count;
        mode = playbackMode;
        past.RemoveAll(entry => IndexOf(entry.Track) < 0);
        if (current == null || IndexOf(Current) < 0)
        {
            Clear();
            return;
        }
        current = Seed(Current);
        future.Clear();
        FillFuture();
    }

    public void Clear()
    {
        current = null;
        past.Clear();
        future.Clear();
    }

    public T EnsureCurrent()
    {
        if (current == null && tracks.Count > 0)
            Select(tracks[mode == PlaybackMode.Shuffle ? Random.Shared.Next(tracks.Count) : 0]);
        return Current;
    }

    public T Select(T track)
    {
        if (IndexOf(track) < 0) return null;
        if (ReferenceEquals(Current, track)) return track;
        if (current != null) RememberCurrent();
        current = Seed(track);
        future.Clear();
        FillFuture();
        return Current;
    }

    public T Move(int direction)
    {
        if (current == null) return EnsureCurrent();
        if (direction > 0)
        {
            RememberCurrent();
            current = future[0];
            future.RemoveAt(0);
        }
        else
        {
            if (future.Count == NeighborLimit) future.RemoveAt(future.Count - 1);
            future.Insert(0, current);
            if (past.Count > 0)
            {
                current = past[^1];
                past.RemoveAt(past.Count - 1);
            }
            else
            {
                int index = IndexOf(Current);
                if (mode == PlaybackMode.Shuffle && tracks.Count > 1)
                {
                    int candidate = Random.Shared.Next(tracks.Count - 1);
                    index = candidate >= index ? candidate + 1 : candidate;
                }
                else index = (index - 1 + tracks.Count) % tracks.Count;
                current = Seed(tracks[index]);
            }
        }
        FillFuture();
        return Current;
    }

    public T Complete() => mode switch
    {
        PlaybackMode.RepeatOne => EnsureCurrent(),
        PlaybackMode.RepeatList or PlaybackMode.Shuffle => Move(1),
        _ => null
    };

    private void RememberCurrent()
    {
        if (past.Count == NeighborLimit) past.RemoveAt(0);
        past.Add(current);
    }

    private Entry Seed(T track) => new(track, NextSeed(), tracks.Count - 1);

    private void FillFuture()
    {
        if (current == null) return;
        while (future.Count < NeighborLimit)
            future.Add(GenerateNext(future.Count == 0 ? current : future[^1]));
    }

    private Entry GenerateNext(Entry previous)
    {
        int index = IndexOf(previous.Track);
        if (mode != PlaybackMode.Shuffle)
            return new(tracks[(index + 1) % tracks.Count], 0, 0);

        // Rank indices by a bijective seeded hash instead of keeping a full shuffle bag.
        // A seed and remaining count are enough to visit each track once per round.
        // Each retained entry carries its cursor so trimming future entries is safe.
        ulong seed = previous.Remaining > 0 ? previous.Seed : NextSeed();
        int remaining = previous.Remaining > 0 ? previous.Remaining : tracks.Count;
        ulong cursor = Rank(index, seed);
        int first = -1, next = -1;
        ulong firstRank = ulong.MaxValue, nextRank = ulong.MaxValue;
        for (int i = 0; i < tracks.Count; i++)
        {
            ulong rank = Rank(i, seed);
            if (first < 0 || rank < firstRank) { first = i; firstRank = rank; }
            if (rank > cursor && (next < 0 || rank < nextRank)) { next = i; nextRank = rank; }
        }
        return new(tracks[next < 0 ? first : next], seed, remaining - 1);
    }

    private static ulong NextSeed() => (ulong)Random.Shared.NextInt64();
    private static ulong Rank(int index, ulong seed)
    {
        unchecked
        {
            ulong value = (ulong)index + seed + 0x9e3779b97f4a7c15UL;
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return value ^ (value >> 31);
        }
    }

    private int IndexOf(T track)
    {
        for (int i = 0; i < tracks.Count; i++)
            if (ReferenceEquals(tracks[i], track)) return i;
        return -1;
    }
}
