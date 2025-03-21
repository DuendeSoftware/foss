// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace WorkerService;

public class WorkerHttpClient : BackgroundService
{
    private readonly ILogger<WorkerHttpClient> _logger;
    private readonly IHttpClientFactory _clientFactory;

    public WorkerHttpClient(ILogger<WorkerHttpClient> logger, IHttpClientFactory factory)
    {
        _logger = logger;
        _clientFactory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("\n\n");
            _logger.LogInformation("WorkerHttpClient running at: {time}", DateTimeOffset.Now);

            var client = _clientFactory.CreateClient("client");
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

            await Task.Delay(5000, stoppingToken);
        }
    }
}
