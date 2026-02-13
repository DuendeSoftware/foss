// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text.Json;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.Internal;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AccessTokenManagement.OpenIdConnect.Internal;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement;

public class TokenRetrieverTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Customizer_can_modify_parameters_when_called_in_client_credentials_token_retriever()
    {
        var overriddenTokenRequestParameters = BuildOverriddenTokenRequestParameters();
        var tokenRequestCustomizer = new TokenRequestCustomizer(overriddenTokenRequestParameters);

        TokenRequestParameters? finalizedTokenRequestParameters = null;
        var tokenManager = new CapturingClientCredentialsTokenManager(parameters =>
        {
            finalizedTokenRequestParameters = parameters;
        });
        var tokenRetriever = new ClientCredentialsTokenRetriever(tokenManager,
            ClientCredentialsClientName.Parse("unknown"), tokenRequestCustomizer);
        await tokenRetriever.GetTokenAsync(new HttpRequestMessage(), _ct);

        finalizedTokenRequestParameters.ShouldBeEquivalentTo(overriddenTokenRequestParameters);
    }

    [Fact]
    public async Task Customizer_can_modify_parameters_when_called_in_open_id_client_access_token_retriever()
    {
        var overriddenTokenRequestParameters = BuildOverriddenUserTokenRequestParametersForClient();
        var tokenRequestCustomizer = new TokenRequestCustomizer(overriddenTokenRequestParameters);

        UserTokenRequestParameters? finalizedTokenRequestParameters = null;
        var tokenManager = new CapturingClientCredentialsTokenManager(parameters =>
        {
            finalizedTokenRequestParameters = parameters as UserTokenRequestParameters;
        });

        var options = Options.Create(new UserTokenManagementOptions());

        var defaultTokenRequestParameters = BuildDefaultUserTokenRequestParameters();
        var tokenRetriever = new OpenIdConnectClientAccessTokenRetriever(tokenManager, options,
            new TestSchemeProvider(), defaultTokenRequestParameters, tokenRequestCustomizer);
        await tokenRetriever.GetTokenAsync(new HttpRequestMessage(), _ct);

        finalizedTokenRequestParameters.ShouldBeEquivalentTo(overriddenTokenRequestParameters);
    }

    [Fact]
    public async Task Customizer_can_modify_parameters_when_called_in_open_id_user_access_token_retriever()
    {
        var overriddenTokenRequestParameters = BuildOverriddenUserTokenRequestParameters();
        var tokenRequestCustomizer = new TokenRequestCustomizer(overriddenTokenRequestParameters);

        UserTokenRequestParameters? finalizedTokenRequestParameters = null;
        var tokenManager = new CapturingUserTokenManager(parameters =>
        {
            finalizedTokenRequestParameters = parameters as UserTokenRequestParameters;
        });
        var defaultTokenRequestParameters = BuildDefaultUserTokenRequestParameters();
        var tokenRetriever = new OpenIdConnectUserAccessTokenRetriever(new UserAccessor(), tokenManager,
            defaultTokenRequestParameters, tokenRequestCustomizer);
        await tokenRetriever.GetTokenAsync(new HttpRequestMessage(), _ct);

        finalizedTokenRequestParameters.ShouldBeEquivalentTo(overriddenTokenRequestParameters);
    }

    private TokenRequestParameters BuildOverriddenTokenRequestParameters() =>
        new()
        {
            Scope = Scope.Parse("overridden-scope"),
            Resource = Resource.Parse("overridden-resource"),
            Assertion = new ClientAssertion
            {
                Type = "overridden-assertion-type",
                Value = "overridden-assertion-value"
            },
            Context = new Parameters(new KeyValuePair<string, string>[]
            {
                new("overridden-context-1-key", "overridden-context-1-value"),
                new("overridden-context-2-key", "overridden-context-2-value")
            }),
            Parameters = new Parameters(new KeyValuePair<string, string>[]
            {
                new("overridden-parameters-1-key", "overridden-parameters-1-value"),
                new("overridden-parameters-2-key", "overridden-parameters-2-value")
            }),
            ForceTokenRenewal = true
        };

    private UserTokenRequestParameters BuildDefaultUserTokenRequestParameters()
    {
        var tokenRequestParameters = new TokenRequestParameters
        {
            Scope = Scope.Parse("scope"),
            Resource = Resource.Parse("resource"),
            Assertion = new ClientAssertion
            {
                Type = "assertion-type",
                Value = "assertion-value"
            },
            Context = new Parameters(new KeyValuePair<string, string>[]
            {
                new("context-1-key", "context-1-value"),
                new("context-2-key", "context-2-value")
            }),
            Parameters = new Parameters(new KeyValuePair<string, string>[]
            {
                new("parameters-1-key", "parameters-1-value"),
                new("parameters-2-key", "parameters-2-value")
            }),
            ForceTokenRenewal = false
        };
        return new UserTokenRequestParameters
        {
            SignInScheme = Scheme.Empty,
            ChallengeScheme = Scheme.Empty,
            Resource = tokenRequestParameters.Resource,
            Assertion = tokenRequestParameters.Assertion,
            Scope = tokenRequestParameters.Scope,
            Context = tokenRequestParameters.Context,
            ForceTokenRenewal = tokenRequestParameters.ForceTokenRenewal,
            Parameters = tokenRequestParameters.Parameters
        };
    }

    private UserTokenRequestParameters BuildOverriddenUserTokenRequestParameters()
    {
        var tokenRequestParameters = BuildOverriddenTokenRequestParameters();
        return new UserTokenRequestParameters
        {
            SignInScheme = Scheme.Empty,
            ChallengeScheme = Scheme.Empty,
            Resource = tokenRequestParameters.Resource,
            Assertion = tokenRequestParameters.Assertion,
            Scope = tokenRequestParameters.Scope,
            Context = tokenRequestParameters.Context,
            ForceTokenRenewal = tokenRequestParameters.ForceTokenRenewal,
            Parameters = tokenRequestParameters.Parameters
        };
    }

    private UserTokenRequestParameters BuildOverriddenUserTokenRequestParametersForClient()
    {
        var tokenRequestParameters = BuildOverriddenTokenRequestParameters();
        return new UserTokenRequestParameters
        {
            ChallengeScheme = Scheme.Empty,
            Resource = tokenRequestParameters.Resource,
            Assertion = tokenRequestParameters.Assertion,
            Scope = tokenRequestParameters.Scope,
            Context = tokenRequestParameters.Context,
            ForceTokenRenewal = tokenRequestParameters.ForceTokenRenewal,
            Parameters = tokenRequestParameters.Parameters
        };
    }

    private class TokenRequestCustomizer(TokenRequestParameters tokenParameters) : ITokenRequestCustomizer
    {
        public Task<TokenRequestParameters> Customize(HttpRequestContext httpRequest,
            TokenRequestParameters baseParameters,
            CancellationToken cancellationToken = default) => Task.FromResult(tokenParameters);
    }

    private class UserAccessor : IUserAccessor
    {
        public Task<ClaimsPrincipal> GetCurrentUserAsync(CancellationToken ct = default) =>
            Task.FromResult(new ClaimsPrincipal());
    }

    private class CapturingUserTokenManager(Action<TokenRequestParameters?> onGetAccessToken) : IUserTokenManager
    {
        public Task<TokenResult<UserToken>> GetAccessTokenAsync(ClaimsPrincipal user,
            UserTokenRequestParameters? parameters = null,
            CancellationToken ct = default)
        {
            onGetAccessToken(parameters);
            var key = CryptoHelper.CreateRsaSecurityKey();
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            jwk.Alg = "ES256";
            var jwkJson = JsonSerializer.Serialize(jwk);
            return Task.FromResult(TokenResult.Success(new UserToken
            {
                Scope = Scope.Parse("unknown"),
                AccessToken = AccessToken.Parse("unknown-access-token"),
                AccessTokenType = AccessTokenType.Parse("Bearer"),
                ClientId = ClientId.Parse("unknown-client-id"),
                DPoPJsonWebKey = DPoPProofKey.ParseOrDefault(jwkJson),
                Expiration = DateTimeOffset.MaxValue
            }));
        }

        public Task RevokeRefreshTokenAsync(ClaimsPrincipal user, UserTokenRequestParameters? parameters = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private class CapturingClientCredentialsTokenManager(Action<TokenRequestParameters?> onGetAccessToken)
        : IClientCredentialsTokenManager
    {
        public Task<TokenResult<ClientCredentialsToken>> GetAccessTokenAsync(ClientCredentialsClientName clientName,
            TokenRequestParameters? parameters = null,
            CancellationToken ct = default)
        {
            onGetAccessToken(parameters);
            var key = CryptoHelper.CreateRsaSecurityKey();
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
            jwk.Alg = "ES256";
            var jwkJson = JsonSerializer.Serialize(jwk);
            return Task.FromResult(TokenResult.Success(new ClientCredentialsToken
            {
                Scope = Scope.Parse("unknown"),
                AccessToken = AccessToken.Parse("unknown-access-token"),
                AccessTokenType = AccessTokenType.Parse("Bearer"),
                ClientId = ClientId.Parse("unknown-client-id"),
                DPoPJsonWebKey = DPoPProofKey.ParseOrDefault(jwkJson),
                Expiration = DateTimeOffset.MaxValue
            }));
        }

        public Task DeleteAccessTokenAsync(ClientCredentialsClientName clientName,
            TokenRequestParameters? parameters = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
