// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebClientAssertions.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUserTokenManager _tokenManager;
    private readonly IDPoPProofService _dPoPProofService;

    public HomeController(IHttpClientFactory httpClientFactory, IUserTokenManager tokenManager, IDPoPProofService dPoPProofService)
    {
        _httpClientFactory = httpClientFactory;
        _tokenManager = tokenManager;
        _dPoPProofService = dPoPProofService;
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

        var url = new Uri("https://demo.duendesoftware.com/api/dpop/test");
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new ("DPoP", token.AccessToken.ToString());

        if (token.DPoPJsonWebKey is { } key)
        {
            var proof = await _dPoPProofService.CreateProofTokenAsync(new DPoPProofRequest
            {
                Url = url,
                Method = HttpMethod.Get,
                DPoPProofKey = key,
                AccessToken = token.AccessToken,
            });

            if (proof is not null)
            {
                request.SetDPoPProofToken(proof.Value);
            }
        }

        var client = _httpClientFactory.CreateClient();
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        ViewBag.Json = PrettyPrint(json);

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
