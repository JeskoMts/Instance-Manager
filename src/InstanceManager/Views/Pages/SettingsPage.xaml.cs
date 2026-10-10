using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using InstanceManager.ViewModels;

namespace InstanceManager.Views.Pages;

public partial class SettingsPage : UserControl
{
    private readonly ListCollectionView _sections;
    private bool _syncingSelection;

    public SettingsPage()
    {
        InitializeComponent();

        _sections = new ListCollectionView(SettingsCatalog.Sections.ToList());
        _sections.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SettingsSection.Group)));
        CategoryList.ItemsSource = _sections;

        CategoryHost.CategoryShown += _ =>
        {
            SettingsScroller.ScrollToVerticalOffset(0);
            SyncSelection();
        };
        Loaded += (_, _) => SyncSelection();
    }

    private void SyncSelection()
    {
        _syncingSelection = true;
        try
        {
            CategoryList.SelectedItem = _sections.Cast<SettingsSection>()
                .FirstOrDefault(section => section.Category == CategoryHost.Category);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingSelection && CategoryList.SelectedItem is SettingsSection section)
            CategoryHost.Category = section.Category;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text;
        _sections.Filter = string.IsNullOrWhiteSpace(query)
            ? null
            : item => SettingsCatalog.Matches((SettingsSection)item, query);

        bool searching = !string.IsNullOrEmpty(query);
        ClearSearchButton.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        NoMatches.Visibility = _sections.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        SyncSelection();

        if (searching && _sections.Count == 1 && _sections.GetItemAt(0) is SettingsSection only)
            CategoryHost.Category = only.Category;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Down))
            return;

        if (_sections.Cast<SettingsSection>().FirstOrDefault() is { } first)
        {
            CategoryHost.Category = first.Category;
            if (e.Key == Key.Down && CategoryList.ItemContainerGenerator.ContainerFromItem(first) is ListBoxItem item)
                item.Focus();
        }
        e.Handled = true;
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }
}
