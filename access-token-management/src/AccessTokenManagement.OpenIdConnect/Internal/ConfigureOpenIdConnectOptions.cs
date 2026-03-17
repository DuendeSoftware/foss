// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.DPoP;

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Configures OpenIdConnectOptions for user token management
/// </summary>
internal class ConfigureOpenIdConnectOptions(
    IDPoPNonceStore dPoPNonceStore,
    IDPoPProofService dPoPProofService,
    IHttpContextAccessor httpContextAccessor,
    IOptions<UserTokenManagementOptions> userAccessTokenManagementOptions,
    IAuthenticationSchemeProvider schemeProvider,
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : IConfigureNamedOptions<OpenIdConnectOptions>
{

    private readonly Scheme _configScheme = GetConfigScheme(userAccessTokenManagementOptions.Value, schemeProvider);

    // Normally, we'd resolve this directly from DI in the constructor. However
    // This would cause a circular dependency since the IClientAssertionService may depend on the OIDC configuration,
    // which is what we're configuring here.
    private IClientAssertionService ClientAssertionService => serviceProvider.GetRequiredService<IClientAssertionService>();

    private ClientCredentialsClientName ClientName => _configScheme.ToClientName();

    private static Scheme GetConfigScheme(UserTokenManagementOptions options, IAuthenticationSchemeProvider schemeProvider)
    {
        var scheme = options.ChallengeScheme;
        if (scheme != null)
        {
            return scheme.Value;
        }

        var defaultScheme = schemeProvider.GetDefaultChallengeSchemeAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        var schemeName = defaultScheme?.Name ?? throw new InvalidOperationException(
            "No OpenID Connect authentication scheme configured for getting client configuration. Either set the scheme name explicitly or set the default challenge scheme");

        return Scheme.Parse(schemeName);
    }

    /// <inheritdoc/>
    public void Configure(OpenIdConnectOptions options)
    {
    }

    /// <inheritdoc/>
    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (_configScheme.ToString() == name)
        {
            // add the event handling to enable DPoP for this OIDC client
            options.Events.OnRedirectToIdentityProvider = CreateCallback(options.Events.OnRedirectToIdentityProvider);
            options.Events.OnAuthorizationCodeReceived = CreateCallback(options.Events.OnAuthorizationCodeReceived);
            options.Events.OnTokenValidated = CreateCallback(options.Events.OnTokenValidated);
#if NET9_0_OR_GREATER
            options.Events.OnPushAuthorization = CreateCallback(options.Events.OnPushAuthorization);
#endif
            options.BackchannelHttpHandler = new AuthorizationServerDPoPHandler(dPoPProofService, dPoPNonceStore, httpContextAccessor, ClientName, loggerFactory)
            {
                InnerHandler = options.BackchannelHttpHandler ?? new HttpClientHandler()
            };
        }
    }

    private Func<RedirectContext, Task> CreateCallback(Func<RedirectContext, Task> inner)
    {
        async Task Callback(RedirectContext context)
        {
            await inner.Invoke(context);

            var dPoPKeyStore = context.HttpContext.RequestServices.GetRequiredService<IDPoPKeyStore>();

            var key = await dPoPKeyStore.GetKeyAsync(ClientName);
            if (key == null)
            {
                return;
            }

            var jkt = dPoPProofService.GetProofKeyThumbprint(key.Value);

            // checking for null allows for opt-out from using DPoP
            if (jkt == null)
            {
                return;
            }

            context.Properties.SetProofKey(key.Value);

            // pass jkt to authorize endpoint
            context.ProtocolMessage.Parameters[OidcConstants.AuthorizeRequest.DPoPKeyThumbprint] =
                jkt.ToString();
            // we store the proof key here to associate it with the
            // authorization code that will be returned. Ultimately we
            // use this to provide proof of possession during code
            // exchange.
        }

        return Callback;
    }

    private Func<AuthorizationCodeReceivedContext, Task> CreateCallback(Func<AuthorizationCodeReceivedContext, Task> inner)
    {
        async Task Callback(AuthorizationCodeReceivedContext context)
        {
            await inner.Invoke(context);

            // get key from storage
            var jwk = context.Properties?.GetProofKey();
            if (jwk != null)
            {
                // set it so the OIDC message handler can find it
                context.HttpContext.SetCodeExchangeDPoPKey(jwk.Value);
            }

            // Automatically send client assertion during code exchange if a service is registered
            var assertion = await ClientAssertionService
                .GetClientAssertionAsync(ClientName, ct: context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (assertion != null && context.TokenEndpointRequest != null)
            {
                context.TokenEndpointRequest.ClientAssertionType = assertion.Type;
                context.TokenEndpointRequest.ClientAssertion = assertion.Value;
            }
        }

        return Callback;
    }

    private Func<TokenValidatedContext, Task> CreateCallback(Func<TokenValidatedContext, Task> inner)
    {
        async Task Callback(TokenValidatedContext context)
        {
            await inner.Invoke(context);

            // TODO: we don't have a good approach for this right now, since the IUserTokenStore
            // just assumes that the session management has been populated with all the token values
            //
            // get key from storage
            //var jwk = context.Properties?.GetProofKey();
            //if (jwk != null)
            //{
            //    // clear this so the properties are not bloated
            //    // and defer to the host and/or IUserTokenStore implementation to decide where the key is kept
            //    //context.Properties!.RemoveProofKey();
            //}
        }

        return Callback;
    }

#if NET9_0_OR_GREATER
    private Func<PushedAuthorizationContext, Task> CreateCallback(Func<PushedAuthorizationContext, Task> inner)
    {
        async Task Callback(PushedAuthorizationContext context)
        {
            await inner.Invoke(context);

            // --- Client assertion ---
            var assertion = await ClientAssertionService
                .GetClientAssertionAsync(ClientName, ct: context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (assertion != null)
            {
                context.ProtocolMessage.ClientAssertionType = assertion.Type;
                context.ProtocolMessage.ClientAssertion = assertion.Value;
                context.HandleClientAuthentication();
            }
        }

        return Callback;
    }
#endif
}
