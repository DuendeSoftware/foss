using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace WpfWebView2
{
    public class WpfEmbeddedBrowser : IBrowser
    {
        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
        {
            var semaphoreSlim = new SemaphoreSlim(0, 1);
            var browserResult = new BrowserResult()
            {
                ResultType = BrowserResultType.UserCancel
            };

            var signinWindow = new Window()
            {
                Width = 800,
                Height = 600,
                Title = "Sign In",
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            signinWindow.Closing += (s, e) =>
            {
                semaphoreSlim.Release();
            };

            var webView = new WebView2();
            webView.NavigationStarting += (s, e) =>
            {
                if (IsBrowserNavigatingToRedirectUri(new Uri(e.Uri), options))
                {
                    e.Cancel = true;

                    browserResult = new BrowserResult()
                    {
                        ResultType = BrowserResultType.Success,
                        Response = new Uri(e.Uri).AbsoluteUri
                    };

                    semaphoreSlim.Release();
                    signinWindow.Close();
                }
            };

            signinWindow.Content = webView;
            signinWindow.Show();

            // Initialization
            await webView.EnsureCoreWebView2Async(null);

            // Delete existing Cookies so previous logins won't remembered
            webView.CoreWebView2.CookieManager.DeleteAllCookies();

            // Navigate
            webView.CoreWebView2.Navigate(options.StartUrl);

            await semaphoreSlim.WaitAsync();

            return browserResult;
        }

        private bool IsBrowserNavigatingToRedirectUri(Uri uri, BrowserOptions options)
        {
            return uri.AbsoluteUri.StartsWith(options.EndUrl);
        }
    }
}
