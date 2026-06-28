using System.Windows;

namespace InstanceManager.Behaviors;

internal static class ThemeReorderAnimation
{
    internal static IReadOnlyDictionary<T, Vector> CalculateOffsets<T>(
        IReadOnlyDictionary<T, Point> before,
        IReadOnlyDictionary<T, Point> after)
        where T : notnull
    {
        var offsets = new Dictionary<T, Vector>();
        foreach ((T item, Point oldPosition) in before)
        {
            if (!after.TryGetValue(item, out Point newPosition))
                continue;

            Vector offset = oldPosition - newPosition;
            if (offset.LengthSquared > 0.01)
                offsets[item] = offset;
        }

        return offsets;
    }
}
