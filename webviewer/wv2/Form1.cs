using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Windows.Forms.AxHost;

namespace wv2
{
    public partial class Form1 : Form
    {
        private WebView2 webView;

        public Form1()
        {
            InitializeComponent();
            webView = new WebView2();
            //webView.Source = new Uri("https://www.microsoft.com");
            //webView.Source = new Uri(Path.GetFullPath("app.html"));
            //webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            webView.Dock = DockStyle.Fill;

            var btn = new Button();
            btn.Text = "sent";
            btn.Click += Btn_Click;

            var tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.Controls.Add(btn, 0, 0);
            tbl.Controls.Add(webView, 0, 1);
            Controls.Add(tbl);

            InitializeAsync();
        }

        private void Btn_Click(object? sender, EventArgs e)
        {
            var uri = new Uri(Path.GetFullPath("assets/ConnectCamera.html"));
            webView.CoreWebView2.Navigate(uri.AbsoluteUri);
            //webView.CoreWebView2.Navigate("https://www.microsoft.com");
            //webView.CoreWebView2.ExecuteScriptAsync($"notify('{uri} is not safe, try an https link')");
        }

        private void WebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            //webView.CoreWebView2.Navigate("https://www.microsoft.com");
        }

        async void InitializeAsync()
        {


            var environment = await CoreWebView2Environment.CreateAsync();
            await webView.EnsureCoreWebView2Async(environment);

            // Map the local folder to a virtual host name
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets.example", Path.GetFullPath("assets"),
                CoreWebView2HostResourceAccessKind.Allow);

            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.WebMessageReceived += MsgHandler;
            // CoreWebView2PermissionKind Camera
            var settings = webView.CoreWebView2.Settings;
            settings.AreHostObjectsAllowed = false;
            settings.IsWebMessageEnabled = true;
            settings.IsScriptEnabled = true; // Enable only if necessary
            webView.CoreWebView2.PermissionRequested += HandlePermissionRequested;

            // Load content using the virtual URL
            webView.Source = new Uri("https://appassets.example/ConnectCamera.html");
            


            //await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.postMessage(window.document.URL);");
            //await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.chrome.webview.addEventListener(\'message\', event => alert(event.data));");

        }

        private void HandlePermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            if (e.PermissionKind == CoreWebView2PermissionKind.Camera)
            {
                e.State = CoreWebView2PermissionState.Allow;
            }
        }

        private void MsgHandler(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            String json = args.TryGetWebMessageAsString();
            //webView.CoreWebView2.ExecuteScriptAsync($"notify('{uri} is not safe, try an https link')");

            var msg = JsonSerializer.Deserialize<Msg>(json);
            switch (msg?.funcName)
            {
                case "OnState":
                    if (msg.data == "script_loaded")
                    {

                        EvalCmd("receiveLoginFromNet(\"OK\");");
                        EvalCmd("handleBarcode(\"7230B086XA\");");
                        EvalCmd("startCamera_in();");
                    }
                    break;
                case "devices":
                    break;
            }
        }
        private void EvalCmd(string cmd)
        {
            try
            {
                webView.CoreWebView2.ExecuteScriptAsync(cmd);
            }
            catch (Exception ex)
            {
                Logger.Error($"exec [{cmd}] error [{ex.Message}]");
            }
        }
    }

    class Msg
    {
        public string? funcName { get; set; }
        public string? data { get; set; }
    }
}