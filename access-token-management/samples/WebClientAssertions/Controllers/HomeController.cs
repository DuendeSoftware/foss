// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebClientAssertions.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserTokenManager _tokenManager;

    public HomeController(IHttpClientFactory httpClientFactory, IUserTokenManager tokenManager)
    {
        _httpClientFactory = httpClientFactory;
        _tokenManager = tokenManager;
    }

    [AllowAnonymous]
    public IActionResult Index() => View();

    public IActionResult Secure() => View();

    public IActionResult Logout() => SignOut("cookie", "oidc");

    [AllowAnonymous]
    public IActionResult Login() => Challenge(new AuthenticationProperties { RedirectUri = "/" });

    // -----------------------------------------------------------------------
    //  User token endpoints (DPoP + JWT client assertion)
    // -----------------------------------------------------------------------

    public async Task<IActionResult> CallApiAsUserManual()
    {
        var token = await _tokenManager.GetAccessTokenAsync(User).GetToken();
        var client = _httpClientFactory.CreateClient();
        client.SetBearerToken(token.AccessToken.ToString()!);

        var response = await client.GetStringAsync("https://demo.duendesoftware.com/api/dpop/test");
        ViewBag.Json = PrettyPrint(response);

        return View("CallApi");
    }

    public async Task<IActionResult> CallApiAsUserFactory()
    {
        var client = _httpClientFactory.CreateClient("user_client");
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

    // -----------------------------------------------------------------------
    //  Client token endpoints (M2M / client credentials + JWT assertion)
    // -----------------------------------------------------------------------

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

    private static string PrettyPrint(string json)
    {
        var doc = JsonDocument.Parse(json).RootElement;
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
