// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.Browser;

namespace Duende.IdentityModel.OidcClient;

public class AuthorizeRequestTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    [Fact]
    public void Default_parameters_should_be_used_for_authorize_request()
    {
        var options = new OidcClientOptions
        {
            ClientId = "client_id",
            Scope = "openid",
            Resource = { "urn:resource1", "urn:resource2" },
            RedirectUri = "http://redirect"
        };

        var client = new AuthorizeClient(options);
        var parameters = client.CreateAuthorizeParameters("state", "code_challenge", null);

        parameters.Count.ShouldBe(9);
        parameters.GetValues("client_id").Single().ShouldBe("client_id");
        parameters.GetValues("scope").Single().ShouldBe("openid");
        parameters.GetValues("resource").First().ShouldBe("urn:resource1");
        parameters.GetValues("resource").Skip(1).First().ShouldBe("urn:resource2");
        parameters.GetValues("redirect_uri").Single().ShouldBe("http://redirect");
        parameters.GetValues("response_type").Single().ShouldBe("code");
        parameters.GetValues("state").Single().ShouldBe("state");
        parameters.GetValues("code_challenge").Single().ShouldBe("code_challenge");
        parameters.GetValues("code_challenge_method").Single().ShouldBe("S256");
    }

    [Fact]
    public void Missing_default_parameters_can_be_set_by_extra_parameters()
    {
        var options = new OidcClientOptions();

        var frontChannel = new Parameters
        {
            { "resource", "urn:resource1" },
            { "resource", "urn:resource2" },

            { "client_id", "client_id2" },
            { "scope", "openid extra" },
            { "redirect_uri", "http://redirect2" }
        };

        var client = new AuthorizeClient(options);
        var parameters = client.CreateAuthorizeParameters("state", "code_challenge", frontChannel);

        parameters.Count.ShouldBe(9);
        parameters.GetValues("client_id").Single().ShouldBe("client_id2");
        parameters.GetValues("scope").Single().ShouldBe("openid extra");
        parameters.GetValues("redirect_uri").Single().ShouldBe("http://redirect2");
        parameters.GetValues("response_type").Single().ShouldBe("code");
        parameters.GetValues("state").Single().ShouldBe("state");
        parameters.GetValues("code_challenge").Single().ShouldBe("code_challenge");
        parameters.GetValues("code_challenge_method").Single().ShouldBe("S256");

        var resources = parameters.GetValues("resource").ToList();
        resources.Count.ShouldBe(2);
        resources[0].ShouldBe("urn:resource1");
        resources[1].ShouldBe("urn:resource2");
    }

    [Fact]
    public async Task Browser_error_is_surfaced_in_authorize_response()
    {
        var options = new OidcClientOptions
        {
            ClientId = "client_id",
            Scope = "openid",
            RedirectUri = "http://redirect",
            ProviderInformation = new ProviderInformation
            {
                AuthorizeEndpoint = "https://authority/authorize"
            },

            Browser = new TestBrowser(_ => Task.FromResult(new BrowserResult
            {
                ResultType = BrowserResultType.HttpError,
                Error = "Something terrible happened",
                ErrorDescription = "Explaining the terrible error..."
            }))
        };

        var client = new AuthorizeClient(options);

        var response = await client.AuthorizeAsync(new AuthorizeRequest(), _ct);

        response.Error.ShouldBe("Something terrible happened");
        response.ErrorDescription.ShouldBe("Explaining the terrible error...");
    }
}
