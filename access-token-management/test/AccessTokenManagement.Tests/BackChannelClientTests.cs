// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
namespace Duende.AccessTokenManagement.Tests;

public class BackChannelClientTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Get_access_token_uses_default_backchannel_client_from_factory()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = "https://as";
                client.ClientId = "id";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.NotFound);

        services.AddHttpClient(ClientCredentialsTokenManagementDefaults.BackChannelHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

        var token = await sut.GetAccessTokenAsync("test");

        token.AccessToken.ShouldBeNull();
        token.AccessTokenType.ShouldBeNull();
        token.Error.ShouldBe("Not Found");
        mockHttp.GetMatchCount(request).ShouldBe(1);
    }

    [Fact]
    public async Task Get_access_token_uses_custom_backchannel_client_from_factory()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = "https://as";
                client.ClientId = "id";

                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.NotFound);

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

        var token = await sut.GetAccessTokenAsync("test");

        token.AccessToken.ShouldBeNull();
        token.AccessTokenType.ShouldBeNull();
        token.Error.ShouldBe("Not Found");
        mockHttp.GetMatchCount(request).ShouldBe(1);
    }
    [Fact]
    public async Task Getting_a_token_with_different_scope_twice_sequentially_will_result_in_two_calls()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = "https://as";
                client.ClientId = "id";

                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new TokenResponse()
            {

            }));

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

        ClientCredentialsToken token1 = null!;
        token1 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
        {
            ForceRenewal = false,
            Scope = "scope1",

        });


        ClientCredentialsToken token2 = null!;

        token2 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
        {
            ForceRenewal = false,
            Scope = "scope2",

        });

        mockHttp.GetMatchCount(request).ShouldBe(2);

    }

    [Fact]
    public async Task Getting_a_token_with_different_scope_twice_concurrently_will_result_two_calls()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = "https://as";
                client.ClientId = "id";

                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new TokenResponse()
            {

            }));

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        mockHttp.AutoFlush = false;

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

        ClientCredentialsToken token1 = null!;
        var t1 = Task.Run(async () =>
        {
            token1 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceRenewal = false,
                Scope = "scope1",

            });
        });
        await Task.Delay(100);


        ClientCredentialsToken token2 = null!;
        var t2 = Task.Run(async () =>
        {

            token2 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceRenewal = false,
                Scope = "scope2",

            });
        });


        output.WriteLine("before delay");

        await Task.Delay(100);

        mockHttp.Flush();
        output.WriteLine("flushed");
        await t1.ThrowOnTimeout();
        await t2.ThrowOnTimeout();

        mockHttp.GetMatchCount(request).ShouldBe(2);

    }

    [Fact]
    public async Task Getting_a_token_with_different_parameters_twice_concurrently_will_result_two_calls()
    {
        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = "https://as";
                client.ClientId = "id";

                client.HttpClientName = "custom";
            });

        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.OK, JsonContent.Create(new TokenResponse()
            {

            }));

        services.AddHttpClient("custom")
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        mockHttp.AutoFlush = true;

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

        ClientCredentialsToken token1 = null!;
        var t1 = Task.Run(async () =>
        {
            token1 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceRenewal = false,
                Parameters = new Parameters()
                {
                    {"tenant", "1"}
                }

            });
        });
        await Task.Delay(100);


        ClientCredentialsToken token2 = null!;
        var t2 = Task.Run(async () =>
        {

            token2 = await sut.GetAccessTokenAsync("test", new TokenRequestParameters()
            {
                ForceRenewal = false,
                Parameters = new Parameters()
                {
                    {"tenant", "2"}
                }

            });
        });


        output.WriteLine("before delay");

        await Task.Delay(100);

        mockHttp.Flush();
        output.WriteLine("flushed");
        await t1.ThrowOnTimeout();
        await t1.ThrowOnTimeout();

        mockHttp.GetMatchCount(request).ShouldBe(1);

    }
    [Fact]
    public async Task Get_access_token_uses_specific_http_client_instance()
    {
        var mockHttp = new MockHttpMessageHandler();
        var request = mockHttp.When("https://as/*")
            .Respond(HttpStatusCode.NotFound);
        var mockClient = mockHttp.ToHttpClient();

        var services = new ServiceCollection();

        services.AddClientCredentialsTokenManagement()
            .AddClient("test", client =>
            {
                client.TokenEndpoint = "https://as";
                client.ClientId = "id";

                client.HttpClient = mockClient;
            });

        var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IClientCredentialsTokenManagementService>();

        var token = await sut.GetAccessTokenAsync("test");

        token.AccessToken.ShouldBeNull();
        token.AccessTokenType.ShouldBeNull();
        token.Error.ShouldBe("Not Found");
        mockHttp.GetMatchCount(request).ShouldBe(1);
    }
}


public static class TimeoutExtensions
{
    public static async Task<T> ThrowOnTimeout<T>(this Task<T> task, TimeSpan timeout = default, string? message = null)
    {
        Stopwatch sw = Stopwatch.StartNew();
        timeout = GetTimeOutOrDefault(timeout);

        using (var cts = new CancellationTokenSource())
        {
            var delayTask = Task.Delay(timeout, cts.Token);

            var resultTask = await Task.WhenAny(task, delayTask);
            if (resultTask == delayTask)
                // Operation cancelled
                throw new OperationCanceledException((message ?? "operation cancelled") + " after " + sw.ElapsedMilliseconds + "ms");
            cts.Cancel();

            return await task;
        }
    }

    private static TimeSpan GetTimeOutOrDefault(TimeSpan timeout)
    {
        if (Debugger.IsAttached) return TimeSpan.FromMinutes(5);

        return timeout == default ? TimeSpan.FromSeconds(2) : timeout;
    }

    public static async Task ThrowOnTimeout(this Task task, TimeSpan timeout = default)
    {
        timeout = GetTimeOutOrDefault(timeout);

        using (var cts = new CancellationTokenSource())
        {
            var delayTask = Task.Delay(timeout, cts.Token);

            var resultTask = await Task.WhenAny(task, delayTask);
            if (resultTask == delayTask)
                // Operation cancelled
                throw new OperationCanceledException();
            cts.Cancel();

            await task;
        }
    }
}