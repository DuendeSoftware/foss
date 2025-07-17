using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.DPoP;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private HttpClient? _apiClient;
    private OidcClient? _oidcClient;
    private AuthorizeState? _state;

    public MainWindow()
    {
        InitializeComponent();
        
        var proofKey = InitializeOidcClient();

        var session = Session.Get();
        if (session?.RefreshToken != null)
        {
            InitializeApiClient(proofKey, session.RefreshToken);

            var name = session.Claims.Single(c => c.Type == "name").Value;
            DisplayLoginMessage($"User previously logged in: {name}");
        }
        else
        {
            Message.Text = "Not logged in";
            EnableButtons(isLoggedIn: false);
        }

    }

    public void DisplayLoginMessage(string message)
    {
        Message.Text = message;
        EnableButtons(isLoggedIn: true);
        Activate();
    }

    private async void CallApi_Click(object sender, RoutedEventArgs e)
    {
        if (_apiClient == null) throw new InvalidOperationException("api client uninitialized");
        Details.Text = "Loading...";
        var response = await _apiClient.GetAsync("");

        if (response.IsSuccessStatusCode)
        {
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Details.Text = json.RootElement.ToString();
        }
        else
        {
            Details.Text = $"Error: {response.ReasonPhrase}";
        }
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (_oidcClient == null) throw new InvalidOperationException("oidc client uninitialized");
        var session = Session.Get();
        var url = await _oidcClient.PrepareLogoutAsync(new LogoutRequest
        {
            IdTokenHint = session?.IdToken
        });
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
        await ReceivePostLogoutCallback();
    }

    private async Task ReceivePostLogoutCallback()
    {
        var session = Session.Get();
        if (session != null)
        {
            var callbackManager = new CallbackManager(session.Claims.First(c => c.Type == "sid").Value);
            var response = await callbackManager.RunServer();
            if(response != null)
            {
                Activate();
                Session.Delete();
                Message.Text = "Logged out";
                EnableButtons(isLoggedIn: false);
            }
            else
            {
                Message.Text = "Logout issue - null response from callback"; // Don't think this is possible
            }
        }
        else 
        {
            Message.Text = "Logout issue - no session";
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (_oidcClient == null) throw new InvalidOperationException("oidc client uninitialized");
        _state = await _oidcClient.PrepareLoginAsync();

        // open system browser to start authentication
        OpenBrowser(_state.StartUrl);

        // Wait for the user to login
        var result = await ReceiveSignInCallback();

        InitializeApiClient(result);
        Session.Store(result);
        DisplayLoginMessage($"User logged in: {result.User.Identity?.Name}");
    }

    private void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private string InitializeOidcClient()
    {
        // Configure OidcClient and prepare a login request
        var options = new OidcClientOptions()
        {
            Authority = "https://demo.duendesoftware.com/",
            ClientId = "interactive.public",
            Scope = "openid profile email offline_access",
            RedirectUri = App.SigninCallback,
            PostLogoutRedirectUri = App.SignoutCallback
        };

        // Enable DPoP
        var proofKey = GetProofKey();
        options.ConfigureDPoP(proofKey);

        _oidcClient = new OidcClient(options);
        return proofKey;
    }

    private void InitializeApiClient(string proofKey, string refreshToken)
    {
        if (_oidcClient == null) throw new InvalidOperationException("oidc client uninitialized");
        // Use saved refresh token
        var handler = _oidcClient.CreateDPoPHandler(proofKey, refreshToken);
        _apiClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(App.Api)
        };
    }

    private void InitializeApiClient(LoginResult result)
    {
        _apiClient = new HttpClient(result.RefreshTokenHandler)
        {
            BaseAddress = new Uri(App.Api)
        };
    }

    private async Task<LoginResult> ReceiveSignInCallback()
    {
        if (_oidcClient == null) throw new InvalidOperationException("oidc client uninitialized");
        if (_state == null) throw new InvalidOperationException("state uninitialized");

        var callbackManager = new CallbackManager(_state.State);
        var response = await callbackManager.RunServer();
        return await _oidcClient.ProcessResponseAsync(response, _state);
    }

    private string GetProofKey()
    {
        if (File.Exists("proofkey"))
        {
            var protectedKey = File.ReadAllText("proofkey");
            return DataProtector.Unprotect(protectedKey);
        }

        var proofKey = JsonWebKeys.CreateRsaJson();
        File.WriteAllText("proofkey", DataProtector.Protect(proofKey));
        return proofKey;
    }

    private void EnableButtons(bool isLoggedIn)
    {
        Login.IsEnabled = !isLoggedIn;
        Logout.IsEnabled = isLoggedIn;
        CallApi.IsEnabled = isLoggedIn;
    }
}
