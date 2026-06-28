using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using InstanceManager.Models;
using InstanceManager.Services;
using Microsoft.Web.WebView2.Core;

namespace InstanceManager.Views;

public partial class AddAccountWindow : Window
{
    private const string LoginUrl = "https://www.roblox.com/login";
    private const string RobloxDomain = "https://www.roblox.com";

    private readonly RobloxAuthService _auth;
    private readonly DpapiSecureStore _secure;
    private bool _capturing;
    private bool _ready;
    private bool _closingAfterCleanup;
    private Task? _sessionCleanup;

    public Account? Result { get; private set; }

    public AddAccountWindow(RobloxAuthService auth, DpapiSecureStore secure)
    {
        _auth = auth;
        _secure = secure;
        InitializeComponent();
        Loaded += AddAccountWindow_Loaded;
        Closing += AddAccountWindow_Closing;
        Closed += AddAccountWindow_Closed;
    }

    public static Task<Account?> RunAsync(Window owner, RobloxAuthService auth, DpapiSecureStore secure)
    {
        var window = new AddAccountWindow(auth, secure) { Owner = owner };
        window.ShowDialog();
        return Task.FromResult(window.Result);
    }

    private async void AddAccountWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= AddAccountWindow_Loaded;
        await InitializeWebViewAsync();
    }

    private async void WebView_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e) =>
        await TryCaptureAsync(manual: false);

    private async void AddAccountWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_closingAfterCleanup)
            return;

        e.Cancel = true;
        _ready = false;
        await ClearSessionAsync();
        _closingAfterCleanup = true;
        Close();
    }

    private void AddAccountWindow_Closed(object? sender, EventArgs e)
    {
        Loaded -= AddAccountWindow_Loaded;
        Closing -= AddAccountWindow_Closing;
        Closed -= AddAccountWindow_Closed;
        _ready = false;

        try
        {
            if (WebView.CoreWebView2 is { } core)
            {
                core.NavigationCompleted -= WebView_NavigationCompleted;
                core.NavigationStarting -= WebView_NavigationStarting;
                core.NewWindowRequested -= WebView_NewWindowRequested;
                core.DownloadStarting -= WebView_DownloadStarting;
                core.LaunchingExternalUriScheme -= WebView_LaunchingExternalUriScheme;
                core.CookieManager.DeleteAllCookies();
                core.Stop();
            }
        }
        catch
        {
        }

        try { WebView.Dispose(); }
        catch { }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            AppPaths.CleanupLegacyWebViewData();
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppPaths.WebViewDirectory);

            CoreWebView2ControllerOptions controllerOptions =
                environment.CreateCoreWebView2ControllerOptions();
            controllerOptions.ProfileName = "InstanceManagerLogin";
            controllerOptions.IsInPrivateModeEnabled = true;
            await WebView.EnsureCoreWebView2Async(environment, controllerOptions);

            CoreWebView2 core = WebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsStatusBarEnabled = false;

            core.NavigationStarting += WebView_NavigationStarting;
            core.NavigationCompleted += WebView_NavigationCompleted;
            core.NewWindowRequested += WebView_NewWindowRequested;
            core.DownloadStarting += WebView_DownloadStarting;
            core.LaunchingExternalUriScheme += WebView_LaunchingExternalUriScheme;
            core.Navigate(LoginUrl);
            _ready = true;
            SetStatus("Sign in to Roblox below…");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void WebView_NavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (!RobloxWebViewPolicy.IsAllowedTopLevelNavigation(e.Uri))
            e.Cancel = true;
    }

    private static void WebView_NewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs e) =>
        e.Handled = true;

    private static void WebView_DownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
        e.Handled = true;
    }

    private static void WebView_LaunchingExternalUriScheme(
        object? sender,
        CoreWebView2LaunchingExternalUriSchemeEventArgs e) =>
        e.Cancel = true;

    private async Task TryCaptureAsync(bool manual)
    {
        if (_capturing || !_ready)
            return;
        _capturing = true;
        try
        {
            var cookies = await WebView.CoreWebView2.CookieManager.GetCookiesAsync(RobloxDomain);
            CoreWebView2Cookie? roblox = cookies.FirstOrDefault(
                c => string.Equals(c.Name, ".ROBLOSECURITY", StringComparison.OrdinalIgnoreCase));

            if (roblox is null || string.IsNullOrWhiteSpace(roblox.Value))
            {
                if (manual) SetStatus("Not signed in yet — please sign in to Roblox first.");
                return;
            }

            SetStatus("Validating account…");
            RobloxUserInfo? info = await _auth.GetUserInfoAsync(roblox.Value);
            if (info is null)
            {
                if (manual) SetStatus("Sign-in not complete — please finish signing in.");
                return;
            }

            Result = new Account
            {
                UserId = info.Id,
                Username = info.Name,
                DisplayName = info.DisplayName,
                EncryptedCookie = _secure.Protect(roblox.Value)
            };

            await ClearSessionAsync();
            _closingAfterCleanup = true;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            SetStatus("Error: " + ex.Message);
        }
        finally
        {
            _capturing = false;
        }
    }

    private Task ClearSessionAsync() => _sessionCleanup ??= ClearSessionCoreAsync();

    private async Task ClearSessionCoreAsync()
    {
        CoreWebView2? core = WebView.CoreWebView2;
        if (core == null)
            return;

        try { core.CookieManager.DeleteAllCookies(); }
        catch { }

        try
        {
            await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.AllProfile);
        }
        catch
        {
        }
    }

    private async void Capture_Click(object sender, RoutedEventArgs e) => await TryCaptureAsync(manual: true);

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _ready = false;
        await ClearSessionAsync();
        _closingAfterCleanup = true;
        DialogResult = false;
    }

    private void ShowError(Exception ex)
    {
        bool runtimeMissing = ex is WebView2RuntimeNotFoundException;
        ErrorText.Text = runtimeMissing
            ? "The Microsoft Edge WebView2 runtime is required for sign-in, but it isn't installed."
            : "WebView2 could not be initialized:\n" + ex.Message;
        DownloadRuntimeButton.Visibility = runtimeMissing ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        SetStatus("Failed to initialize.");
    }

    private void DownloadRuntime_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://developer.microsoft.com/microsoft-edge/webview2/",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
