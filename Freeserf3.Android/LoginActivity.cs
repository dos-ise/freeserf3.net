/*
 * LoginActivity.cs - Ubisoft Connect web login and game data download
 *
 * Launcher activity of freeserf3.net on Android. If the Settlers 3 game
 * data is not present yet, shows the official Ubisoft Connect login page in
 * a full-screen WebView. After the user signs in, the session (ticket +
 * session id) is read from the page's localStorage, renewed under the app
 * id and used to download the game data. Afterwards the map viewer
 * (MainActivity) is started. No password is ever typed into the app itself.
 *
 * freeserf3.net is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Freeserf3.Data;
using Freeserf3.Ubisoft;
using System.Text.Json;
using System.Threading.Tasks;

namespace Freeserf3.Android;

[Activity(Label = "freeserf3.net", MainLauncher = true, Exported = true)]
public class LoginActivity : Activity
{
    const string Tag = "Freeserf3";

    View? loginWebViewLayout;
    WebView? loginWebView;
    LinearLayout? progressLayout;
    ProgressBar? progressBar;
    TextView? statusText;
    LinearLayout? retryLayout;

    public string GameDataPath => System.IO.Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "gamedata");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Log.Debug(Tag, "LoginActivity: OnCreate");

        // If the game data is already present, go straight to the viewer.
        if (new GameData(GameDataPath).IsValid())
        {
            StartViewer();
            return;
        }

        ShowUbisoftLogin();
    }

    // Shows the Ubisoft Connect web login in a full-screen WebView. The user
    // logs in on Ubisoft's official page; the session is then read from the
    // page's localStorage and used for the download. The WebView is added via
    // AddContentView so it covers the whole screen; the back button closes it.
    void ShowUbisoftLogin()
    {
        var webView = new WebView(this);
        webView.Settings.JavaScriptEnabled = true;
        webView.Settings.DomStorageEnabled = true;
        webView.Focusable = true;
        webView.FocusableInTouchMode = true;

        var layout = new FrameLayout(this);
        layout.SetBackgroundColor(Color.Black);
        layout.AddView(webView, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        AddContentView(layout, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        loginWebViewLayout = layout;
        loginWebView = webView;

        webView.SetWebViewClient(new UbisoftWebViewClient(this, layout, webView));
        webView.LoadUrl(UbisoftLogin.LoginUrl);
        webView.RequestFocus();
    }

    // Shows a full-screen "no network" overlay with a retry button. Used when
    // the Ubisoft login page cannot be loaded (e.g. the device is offline).
    internal void ShowRetryOverlay(string message)
    {
        if (retryLayout != null)
            return;

        retryLayout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        retryLayout.SetGravity(GravityFlags.Center);
        retryLayout.SetBackgroundColor(Color.ParseColor("#1a1a2e"));

        var icon = new TextView(this)
        {
            Text = "⚠",
            TextSize = 48,
            Gravity = GravityFlags.Center
        };
        icon.SetTextColor(Color.ParseColor("#ffcc00"));
        retryLayout.AddView(icon);

        var text = new TextView(this)
        {
            Text = message,
            TextSize = 16,
            Gravity = GravityFlags.Center
        };
        text.SetTextColor(Color.ParseColor("#dddddd"));
        var textParams = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        textParams.TopMargin = DpToPx(16);
        textParams.LeftMargin = DpToPx(24);
        textParams.RightMargin = DpToPx(24);
        retryLayout.AddView(text, textParams);

        var retryButton = new Button(this) { Text = "Retry", TextSize = 16 };
        retryButton.Click += (s, e) =>
        {
            RemoveRetryOverlay();
            loginWebView?.Reload();
        };
        var buttonParams = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
        buttonParams.TopMargin = DpToPx(24);
        retryLayout.AddView(retryButton, buttonParams);

        AddContentView(retryLayout, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
    }

    internal void RemoveRetryOverlay()
    {
        if (retryLayout == null)
            return;
        (retryLayout.Parent as ViewGroup)?.RemoveView(retryLayout);
        retryLayout = null;
    }

    // Removes the login WebView from the view hierarchy.
    void RemoveUbisoftLoginView()
    {
        if (loginWebView != null)
        {
            loginWebView.StopLoading();
            loginWebView.Destroy();
            loginWebView = null;
        }
        if (loginWebViewLayout != null)
        {
            (loginWebViewLayout.Parent as ViewGroup)?.RemoveView(loginWebViewLayout);
            loginWebViewLayout = null;
        }
    }

    // Intercepts the back button while the login WebView is shown.
    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (keyCode == Keycode.Back && loginWebViewLayout != null)
        {
            RemoveUbisoftLoginView();
            Finish();
            return true;
        }
        return base.OnKeyDown(keyCode, e);
    }

    // Called when the WebView login captured a session (ticket + session id)
    // from the Ubisoft page. Renews the session under the app id and starts
    // the download.
    internal void OnUbisoftWebLoginSuccess(string ticket, string sessionId, View layout, WebView webView)
    {
        RemoveUbisoftLoginView();
        ShowProgress("Logging in...", 0);

        Task.Run(async () =>
        {
            try
            {
                var loginResult = await UbisoftLogin.RenewSessionAsync(ticket, sessionId);
                RunOnUiThread(() =>
                {
                    if (!loginResult.Success)
                    {
                        ShowError(loginResult.ErrorMessage ?? "Login failed.");
                    }
                    else
                    {
                        StartUbisoftDownload(loginResult.Response!);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(Tag, "Renew session: " + ex);
                RunOnUiThread(() => ShowError(ex.Message));
            }
        });
    }

    // Downloads the Settlers 3 game data from Ubisoft Connect on a
    // background thread.
    void StartUbisoftDownload(UbisoftLoginResponse login)
    {
        ShowProgress("Starting download...", 0);

        Task.Run(async () =>
        {
            try
            {
                var downloader = new Settlers3Downloader(login, GameDataPath);
                int files = await downloader.DownloadAsync((status, percent) =>
                {
                    RunOnUiThread(() => UpdateProgress(status, percent));
                });

                Log.Debug(Tag, $"Download complete: {files} files.");
                RunOnUiThread(() =>
                {
                    ShowProgress($"Download complete ({files} files). Starting map viewer...", 100);
                    StartViewer();
                });
            }
            catch (Exception ex)
            {
                Log.Error(Tag, "Download: " + ex);
                RunOnUiThread(() => ShowError($"Download failed: {ex.Message}"));
            }
        });
    }

    void StartViewer()
    {
        var intent = new Intent(this, typeof(MainActivity));
        StartActivity(intent);
        Finish();
    }

    // Shows a full-screen progress overlay (spinner + status text).
    void ShowProgress(string status, int percent)
    {
        if (progressLayout == null)
        {
            progressLayout = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            progressLayout.SetGravity(GravityFlags.Center);
            progressLayout.SetBackgroundColor(Color.ParseColor("#1a1a2e"));

            progressBar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
            {
                Max = 100
            };
            var barParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            barParams.LeftMargin = DpToPx(32);
            barParams.RightMargin = DpToPx(32);
            progressLayout.AddView(progressBar, barParams);

            statusText = new TextView(this)
            {
                TextSize = 14,
                Gravity = GravityFlags.Center
            };
            statusText.SetTextColor(Color.ParseColor("#dddddd"));
            var statusParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            statusParams.TopMargin = DpToPx(16);
            progressLayout.AddView(statusText, statusParams);

            AddContentView(progressLayout, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        }

        progressBar!.Progress = percent;
        statusText!.Text = status;
    }

    void UpdateProgress(string status, int percent)
    {
        if (progressLayout == null)
            return;
        progressBar!.Progress = percent;
        statusText!.Text = status;
    }

    void ShowError(string message)
    {
        if (progressLayout != null)
        {
            (progressLayout.Parent as ViewGroup)?.RemoveView(progressLayout);
            progressLayout = null;
        }
        ShowUbisoftLogin();
        Toast.MakeText(this, message, ToastLength.Long)?.Show();
    }

    int DpToPx(int dp) => (int)(dp * Resources.DisplayMetrics.Density);
}

// WebView client for the Ubisoft Connect web login. After the user logs in
// on Ubisoft's official page, the session (ticket + session id) is stored in
// the page's localStorage; this client polls for it and hands it back to the
// activity.
class UbisoftWebViewClient : WebViewClient
{
    readonly LoginActivity activity;
    readonly View layout;
    readonly WebView webView;
    readonly Handler checkHandler = new Handler(Looper.MainLooper);
    bool checking;

    public UbisoftWebViewClient(LoginActivity activity, View layout, WebView webView)
    {
        this.activity = activity;
        this.layout = layout;
        this.webView = webView;
    }

    public override void OnPageStarted(WebView view, string? url, Bitmap? favicon)
    {
        base.OnPageStarted(view, url, favicon);
        // A new page is loading: hide any retry overlay from a previous error.
        activity.RemoveRetryOverlay();
    }

    public override void OnPageFinished(WebView view, string? url)
    {
        base.OnPageFinished(view, url);
        CheckForLogin();
    }

    // Network/load errors (e.g. device offline): show a retry overlay instead
    // of the default "webpage not available" page.
    public override void OnReceivedError(WebView view, IWebResourceRequest? request, WebResourceError? error)
    {
        base.OnReceivedError(view, request, error);
        if (request?.IsForMainFrame == true)
        {
            Log.Error("Freeserf3", $"WebView main frame error: {error?.ErrorCode} {error?.Description}");
            activity.ShowRetryOverlay("Cannot reach the Ubisoft login page.\nPlease check your internet connection.");
        }
    }

    // Deprecated overload, still called on some Android versions.
    [Obsolete]
    public override void OnReceivedError(WebView view, ClientError errorCode, string? description, string? failingUrl)
    {
        base.OnReceivedError(view, errorCode, description, failingUrl);
        Log.Error("Freeserf3", $"WebView error: {errorCode} {description} ({failingUrl})");
        activity.ShowRetryOverlay("Cannot reach the Ubisoft login page.\nPlease check your internet connection.");
    }

    public override void OnReceivedSslError(WebView view, SslErrorHandler? handler, global::Android.Net.Http.SslError? error)
    {
        Log.Error("Freeserf3", $"WebView SSL error: {error?.PrimaryError}");
        activity.ShowRetryOverlay("A secure connection to the Ubisoft login page could not be established.");
    }

    void CheckForLogin()
    {
        if (checking)
            return;
        checking = true;

        webView.EvaluateJavascript(CheckLoginJs, new ValueCallback(result =>
        {
            checking = false;
            Log.Debug("Freeserf3", $"Web login check result: {result}");

            if (!string.IsNullOrEmpty(result) && result != "null")
            {
                try
                {
                    // The result may be a JSON object or a quoted JSON
                    // string, depending on the WebView version.
                    string json = result;
                    if (json.StartsWith("\"") && json.EndsWith("\""))
                        json = JsonSerializer.Deserialize<string>(json) ?? json;

                    // The JS returns lowercase property names; deserialize
                    // case-insensitively.
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var session = JsonSerializer.Deserialize<UbisoftWebSession>(json, options);
                    if (session != null && !string.IsNullOrEmpty(session.Ticket) && !string.IsNullOrEmpty(session.SessionId))
                    {
                        Log.Debug("Freeserf3", "Web login session captured.");
                        checkHandler.RemoveCallbacksAndMessages(null);
                        activity.OnUbisoftWebLoginSuccess(session.Ticket, session.SessionId, layout, webView);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Freeserf3", $"Web login parse error: {ex.Message}");
                }
            }

            checkHandler.PostDelayed(CheckForLogin, 500);
        }));
    }

    // Reads the Ubisoft Connect session from the page's localStorage. The
    // session is stored under "PRODloginData" after a successful login;
    // other keys are scanned as a fallback.
    const string CheckLoginJs = @"
        (function() {
            function findSession() {
                var loginData = window.localStorage.getItem('PRODloginData');
                if (loginData) {
                    try {
                        var s = JSON.parse(loginData);
                        if (s.ticket && s.sessionId) return s;
                    } catch (e) {}
                }
                for (var i = 0; i < window.localStorage.length; i++) {
                    var key = window.localStorage.key(i);
                    try {
                        var val = JSON.parse(window.localStorage.getItem(key));
                        if (val && val.ticket && val.sessionId) return val;
                    } catch (e) {}
                }
                return null;
            }
            var s = findSession();
            if (s) {
                return {ticket: s.ticket, sessionId: s.sessionId, userId: s.userId, nameOnPlatform: s.nameOnPlatform};
            }
            return null;
        })();
    ";
}

// Session data captured from the Ubisoft Connect web login.
class UbisoftWebSession
{
    public string? Ticket { get; set; }
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public string? NameOnPlatform { get; set; }
}

// Adapts an Action<string> to Android's IValueCallback (used by
// WebView.EvaluateJavascript).
class ValueCallback : Java.Lang.Object, IValueCallback
{
    readonly Action<string> action;

    public ValueCallback(Action<string> action)
    {
        this.action = action;
    }

    public void OnReceiveValue(Java.Lang.Object? value)
    {
        action?.Invoke(value?.ToString());
    }
}
