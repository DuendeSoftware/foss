// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

var builder = Host.CreateApplicationBuilder();





builder.Services.AddClientCredentialsTokenManagement()
    .AddClient("demo", client =>
    {
        client.TokenEndpoint = new Uri("https://demo.duendesoftware.com/connect/token");

        client.ClientId = ClientId.Parse("m2m.short");
        client.ClientSecret = ClientSecret.Parse("secret");

        client.Scope = Scope.Parse("api");
    })
    ;
builder.Services.AddFusionCache()
    .AsHybridCache();

// Alternatively, to only replace the HybridCache for ATM:
//.AsKeyedHybridCache(ServiceProviderKeys.ClientCredentialsTokenCache);

builder.Services.AddClientCredentialsHttpClient("client", ClientCredentialsClientName.Parse("demo"), client =>
{
    client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
});

builder.Services.AddHostedService<WorkerManual>();

var host = builder.Build();

host.Run();

public class WorkerManual : BackgroundService
{
    private readonly ILogger<WorkerManual> _logger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly IClientCredentialsTokenManager _tokenManagementService;

    public WorkerManual(ILogger<WorkerManual> logger, IHttpClientFactory factory, IClientCredentialsTokenManager tokenManagementService)
    {
        _logger = logger;
        _clientFactory = factory;
        _tokenManagementService = tokenManagementService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(3000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("\n\n");
            _logger.LogInformation("WorkerManual running at: {time}", DateTimeOffset.Now);

            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");

            var token = await _tokenManagementService.GetAccessTokenAsync(ClientCredentialsClientName.Parse("demo"), ct: stoppingToken).GetToken();
            client.SetBearerToken(token.AccessToken.ToString());

            var response = await client.GetAsync("test", stoppingToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(stoppingToken);
                _logger.LogInformation("API response: {response}", content);
            }
            else
            {
                _logger.LogError("API returned: {statusCode}", response.StatusCode);
            }

            await Task.Delay(6000, stoppingToken);
        }
    }
}
