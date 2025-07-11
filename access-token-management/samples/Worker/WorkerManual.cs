// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;

using Duende.IdentityModel.Client;

namespace WorkerService;

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
