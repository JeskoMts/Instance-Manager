using System.Windows;
using InstanceManager.Behaviors;
using InstanceManager.Models;
using Xunit;

namespace InstanceManager.Tests;

public sealed class ThemeDragDecisionTests
{
    [Fact]
    public void DropEffectFor_ThemePayload_RemainsMoveOverDraggedCardItself()
    {
        var theme = new ThemeDefinition { Id = "dark", Name = "Dark" };

        Assert.Equal(DragDropEffects.Move, ThemeDragDecision.DropEffectFor(theme));
        Assert.False(ThemeDragDecision.CanReorder(theme, theme));
    }

    [Fact]
    public void DropEffectFor_MissingThemePayload_IsNone()
    {
        Assert.Equal(DragDropEffects.None, ThemeDragDecision.DropEffectFor(null));
    }

    [Fact]
    public void CanReorder_DifferentThemes_IsTrue()
    {
        var dragged = new ThemeDefinition { Id = "dark", Name = "Dark" };
        var target = new ThemeDefinition { Id = "light", Name = "Light" };

        Assert.True(ThemeDragDecision.CanReorder(dragged, target));
    }
}
