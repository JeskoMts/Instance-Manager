using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using InstanceManager.ViewModels;

namespace InstanceManager.Views.Settings;

public sealed class SettingsCategoryHost : ContentControl
{
    public static readonly DependencyProperty CategoryProperty = DependencyProperty.Register(
        nameof(Category), typeof(SettingsCategory), typeof(SettingsCategoryHost),
        new FrameworkPropertyMetadata(SettingsCategory.General, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCategoryChanged));

    private static readonly DependencyPropertyKey SectionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Section), typeof(SettingsSection), typeof(SettingsCategoryHost),
        new PropertyMetadata(SettingsCatalog.For(SettingsCategory.General)));

    public static readonly DependencyProperty SectionProperty = SectionPropertyKey.DependencyProperty;

    private readonly Dictionary<SettingsCategory, FrameworkElement> _views = new();

    public SettingsCategoryHost()
    {
        Focusable = false;
        Loaded += (_, _) => Show(Category);
        DataContextChanged += (_, e) =>
        {
            foreach (FrameworkElement view in _views.Values)
                view.DataContext = e.NewValue;
        };
    }

    public SettingsCategory Category
    {
        get => (SettingsCategory)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public SettingsSection Section => (SettingsSection)GetValue(SectionProperty);

    public event Action<SettingsCategory>? CategoryShown;

    internal int BuiltCount => _views.Count;

    internal static FrameworkElement Create(SettingsCategory category) => category switch
    {
        SettingsCategory.General => new GeneralSettings(),
        SettingsCategory.Appearance => new AppearanceSettings(),
        SettingsCategory.Notifications => new NotificationSettings(),
        SettingsCategory.Launching => new LaunchSettings(),
        SettingsCategory.Graphics => new GraphicsSettings(),
        SettingsCategory.Reconnect => new ReconnectSettings(),
        SettingsCategory.Updates => new UpdateSettings(),
        SettingsCategory.Privacy => new PrivacySettings(),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    private static void OnCategoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (SettingsCategoryHost)d;
        if (host.IsLoaded)
            host.Show((SettingsCategory)e.NewValue);
    }

    private void Show(SettingsCategory category)
    {
        if (!_views.TryGetValue(category, out FrameworkElement? view))
        {
            view = Create(category);
            view.DataContext = DataContext;
            _views[category] = view;
        }

        SetValue(SectionPropertyKey, SettingsCatalog.For(category));
        if (!ReferenceEquals(Content, view))
        {
            Content = view;
            CategoryShown?.Invoke(category);
        }
    }
}
