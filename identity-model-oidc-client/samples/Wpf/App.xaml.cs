using Duende.IdentityModel.Client;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public const string Api = "https://demo.duendesoftware.com/api/dpop/test";
    public const string CustomUriScheme = "oidcclient-wpf-sample"; // In practice, a short reverse domain name (e.g., com.example.app) is preferred
    public const string SigninCallback = $"{CustomUriScheme}:/signin";
    public const string SignoutCallback = $"{CustomUriScheme}:/signout";

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Any() && e.Args.Length == 1)
        {
            await SendCallbackToMainProcess(e.Args[0]);
            Shutdown();
        }
        else
        {
            // Register custom uri scheme if necessary
            new RegistryConfig(CustomUriScheme).Configure();

            // Display Main Window
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        base.OnStartup(e);
    }

    private async Task SendCallbackToMainProcess(string callbackUrl)
    {
        if (callbackUrl.StartsWith(SigninCallback))
        {
            var response = new AuthorizeResponse(callbackUrl);
            if (!String.IsNullOrWhiteSpace(response.State))
            {
                var callbackManager = new CallbackManager(response.State);
                await callbackManager.RunClient(callbackUrl);
            }
        }
        else if (callbackUrl.StartsWith(SignoutCallback))
        {
            var session = Session.Get();
            if (session != null)
            {
                var sid = session.Claims.First(c => c.Type == "sid").Value;
                var callbackManager = new CallbackManager(sid);

                await callbackManager.RunClient(callbackUrl);
            }
        }
    }
}
