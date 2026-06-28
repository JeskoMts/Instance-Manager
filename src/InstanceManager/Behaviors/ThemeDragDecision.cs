using System.Windows;
using InstanceManager.Models;

namespace InstanceManager.Behaviors;

internal static class ThemeDragDecision
{
    internal static DragDropEffects DropEffectFor(ThemeDefinition? draggedTheme) =>
        draggedTheme is null ? DragDropEffects.None : DragDropEffects.Move;

    internal static bool CanReorder(ThemeDefinition? draggedTheme, ThemeDefinition? targetTheme) =>
        draggedTheme is not null
        && targetTheme is not null
        && !ReferenceEquals(draggedTheme, targetTheme);
}
