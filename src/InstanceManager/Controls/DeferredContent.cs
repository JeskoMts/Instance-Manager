using System;
using System.Windows;
using System.Windows.Controls;

namespace InstanceManager.Controls;

public sealed class DeferredContent : ContentControl
{
    public static readonly DependencyProperty ContentTypeProperty = DependencyProperty.Register(
        nameof(ContentType), typeof(Type), typeof(DeferredContent), new PropertyMetadata(null, OnTriggerChanged));

    public Type? ContentType
    {
        get => (Type?)GetValue(ContentTypeProperty);
        set => SetValue(ContentTypeProperty, value);
    }

    public DeferredContent()
    {
        Loaded += (_, _) =>
        {
            if (Visibility == Visibility.Visible)
                Realize();
        };
    }

    public bool IsRealized => Content != null;

    public event Action<FrameworkElement>? Realized;

    public FrameworkElement? Realize()
    {
        if (Content is FrameworkElement existing)
            return existing;
        if (ContentType == null)
            return null;

        var created = (FrameworkElement)Activator.CreateInstance(ContentType)!;
        Content = created;
        Realized?.Invoke(created);
        return created;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == VisibilityProperty && IsLoaded && Visibility == Visibility.Visible)
            Realize();
    }

    private static void OnTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (DeferredContent)d;
        if (host.IsLoaded && host.Visibility == Visibility.Visible)
            host.Realize();
    }
}
