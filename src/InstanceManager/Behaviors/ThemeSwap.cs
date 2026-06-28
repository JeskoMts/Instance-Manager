using System.Collections.Generic;

namespace InstanceManager.Behaviors;

internal static class ThemeSwap
{
    internal static IReadOnlyList<T> SwapPair<T>(IReadOnlyList<T> baseOrder, T dragged, T target)
    {
        var result = new List<T>(baseOrder);
        int from = result.IndexOf(dragged);
        int to = result.IndexOf(target);
        if (from < 0 || to < 0 || from == to)
            return result;

        (result[from], result[to]) = (result[to], result[from]);
        return result;
    }
}
