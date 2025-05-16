// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;

using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Web.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserTokenManager _tokenManager;
    private readonly SampleConfiguration _configuration;

    public HomeController(IHttpClientFactory httpClientFactory, IUserTokenManager tokenManager, IOptions<SampleConfiguration> options)
    {
        _httpClientFactory = httpClientFactory;
        _tokenManager = tokenManager;
        _configuration = options.Value;
    }

    [AllowAnonymous]
    public IActionResult Index() => View();

    public IActionResult Secure() => View();

    public IActionResult Logout() => SignOut("cookie", "oidc");

    public async Task<IActionResult> CallApiAsUserManual()
    {
        var token = await _tokenManager.GetAccessTokenAsync(User).GetToken();
        var client = _httpClientFactory.CreateClient();
        client.SetToken(token.AccessTokenType?.ToScheme().ToString()!, token.AccessToken.ToString());

        var response = await client.GetStringAsync($"{_configuration.ApiBaseUrl}test");
        ViewBag.Json = PrettyPrint(response);

        return View("CallApi");
    }

    public async Task<IActionResult> CallApiAsUserExtensionMethod()
    {
        var token = await HttpContext.GetUserAccessTokenAsync().GetToken();
        var client = _httpClientFactory.CreateClient();
        var scheme = token.AccessTokenType?.ToScheme().ToString()
            ?? throw new InvalidOperationException("Scheme is empty");
        client.SetToken(scheme, token.AccessToken.ToString());

        var response = await client.GetStringAsync($"{_configuration.ApiBaseUrl}test");
        ViewBag.Json = PrettyPrint(response);

        return View("CallApi");
    }

    public async Task<IActionResult> CallApiAsUserFactory()
    {
        var client = _httpClientFactory.CreateClient("user");

        var response = await client.GetStringAsync("test");
        ViewBag.Json = PrettyPrint(response);

        return View("CallApi");
    }

    public async Task<IActionResult> CallApiAsUserFactoryTyped([FromServices] TypedUserClient client)
    {
        var response = await client.CallApi();
        ViewBag.Json = PrettyPrint(response);

        return View("CallApi");
    }

    [AllowAnonymous]
    public async Task<IActionResult> CallApiAsUserResourceIndicator()
    {
        var client = _httpClientFactory.CreateClient("user-resource");
        var response = await client.GetStringAsync("test");

        ViewBag.Json = PrettyPrint(response);
        return View("CallApi");
    }


    [AllowAnonymous]
    public async Task<IActionResult> CallApiAsClientExtensionMethod()
    {
        var token = await HttpContext.GetClientAccessTokenAsync().GetToken();
        var client = _httpClientFactory.CreateClient();
        var scheme = token.AccessTokenType?.ToScheme().ToString() ?? throw new InvalidOperationException("Scheme is empty");
        client.SetToken(scheme, token.AccessToken.ToString());

        var response = await client.GetStringAsync($"{_configuration.ApiBaseUrl}test");

        ViewBag.Json = PrettyPrint(response);
        return View("CallApi");
    }

    [AllowAnonymous]
    public async Task<IActionResult> CallApiAsClientResourceIndicator()
    {
        var client = _httpClientFactory.CreateClient("client-resource");
        var response = await client.GetStringAsync("test");

        ViewBag.Json = PrettyPrint(response);
        return View("CallApi");
    }


    [AllowAnonymous]
    public async Task<IActionResult> CallApiAsClientFactory()
    {
        var client = _httpClientFactory.CreateClient("client");
        var response = await client.GetStringAsync("test");

        ViewBag.Json = PrettyPrint(response);
        return View("CallApi");
    }

    [AllowAnonymous]
    public async Task<IActionResult> CallApiAsClientFactoryTyped([FromServices] TypedClientClient client)
    {
        var response = await client.CallApi();
        ViewBag.Json = PrettyPrint(response);

        return View("CallApi");
    }

    string PrettyPrint(string json)
    {
        var doc = JsonDocument.Parse(json).RootElement;
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
