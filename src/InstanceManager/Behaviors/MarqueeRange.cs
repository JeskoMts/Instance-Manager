using System;
using System.Collections.Generic;

namespace InstanceManager.Behaviors;

public readonly record struct ItemSpan(int Index, double Top, double Bottom);

public readonly record struct MarqueeAnchor(int First, int Last);

public static class MarqueeRange
{
    public static MarqueeAnchor Locate(IReadOnlyList<ItemSpan> realized, double y)
    {
        if (realized.Count == 0)
            return new MarqueeAnchor(0, -1);

        int lowest = int.MaxValue;
        int highest = int.MinValue;
        foreach (ItemSpan span in realized)
        {
            lowest = Math.Min(lowest, span.Index);
            highest = Math.Max(highest, span.Index);
        }

        int first = highest + 1;
        int last = lowest - 1;
        foreach (ItemSpan span in realized)
        {
            if (span.Bottom > y)
                first = Math.Min(first, span.Index);
            if (span.Top < y)
                last = Math.Max(last, span.Index);
        }

        return new MarqueeAnchor(first, last);
    }

    public static (int From, int To) Between(MarqueeAnchor origin, MarqueeAnchor current, bool currentIsBelow) =>
        currentIsBelow ? (origin.First, current.Last) : (current.First, origin.Last);
}
