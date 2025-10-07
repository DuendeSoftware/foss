// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Duende.AspNetCore.Authentication.OAuth2Introspection.Context;
using Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

/// <summary>
/// Authentication handler for OAuth 2.0 introspection
/// </summary>
public class OAuth2IntrospectionHandler : AuthenticationHandler<OAuth2IntrospectionOptions>
{
    private readonly HybridCache _cache;
    private readonly ILogger<OAuth2IntrospectionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuth2IntrospectionHandler"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="urlEncoder">The URL encoder.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="cache">The cache.</param>
    public OAuth2IntrospectionHandler(
        IOptionsMonitor<OAuth2IntrospectionOptions> options,
        UrlEncoder urlEncoder,
        ILoggerFactory loggerFactory,
        [FromKeyedServices(ServiceProviderKeys.IntrospectionCache)] HybridCache cache)
        : base(options, loggerFactory, urlEncoder)
    {
        _logger = loggerFactory.CreateLogger<OAuth2IntrospectionHandler>();
        _cache = cache;
    }

    /// <summary>
    /// The handler calls methods on the events which give the application control at certain points where processing is occurring.
    /// If it is not provided a default instance is supplied which does nothing when the methods are called.
    /// </summary>
    protected new OAuth2IntrospectionEvents Events
    {
        get => (OAuth2IntrospectionEvents)base.Events!;
        set => base.Events = value;
    }

    /// <inheritdoc/>
    protected override Task<object> CreateEventsAsync() => Task.FromResult<object>(new OAuth2IntrospectionEvents());

    /// <summary>
    /// Tries to authenticate a reference token on the current request
    /// </summary>
    /// <returns></returns>
    [RequiresUnreferencedCode("Calls methods on CacheExtensions that are annotated with RequiresUnreferencedCode.")]
#pragma warning disable IL2046 // This method is properly annotated with RequiresUnreferencedCode, but base class does not have the same annotation.
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
#pragma warning restore IL2046
    {
        var token = Options.TokenRetriever(Context.Request);

        // no token - nothing to do here
        if (token.IsMissing())
        {
            return AuthenticateResult.NoResult();
        }

        // if token contains a dot - it might be a JWT and we are skipping
        // this is configurable
        if (Options.SkipTokensWithDots && token.Contains('.'))
        {
            Log.SkippingDotToken(_logger, null);
            return AuthenticateResult.NoResult();
        }

        try
        {
            var cacheKey = Options.CacheKeyGenerator(Options, token);
            var claims = await _cache.GetOrCreateAsync(cacheKey, async cancel =>
            {
                Log.TokenNotCached(_logger, null);

                var response = await LoadClaimsForToken(token, Context, Scheme, Events, Options);
                if (response.IsError)
                {
                    throw new PreventCacheException($"Error returned from introspection endpoint: {response.Error}");
                }

                var claims = response.Claims.ToList();
                if (!response.IsActive)
                {
                    claims.Add(new Claim(JwtClaimTypes.Expiration, TimeProvider.GetUtcNow().Add(Options.CacheDuration).ToUnixTimeSeconds().ToString()));
                }

                var expClaim = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Expiration);
                var now = TimeProvider.GetUtcNow();
                var expiration = expClaim == null ? now + Options.CacheDuration : DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim.Value));
                Log.TokenExpiresOn(_logger, expiration, null);

                if (expiration <= now)
                {
                    // this seems half correct. we don't want to cache, but the response from the server WAS valid
                    throw new PreventCacheException("Token is already expired");
                }

                //TODO: we somehow need to control the lifetime of the cache entry based on the exp claim

                return claims;

            }).ConfigureAwait(false);

            if (claims.Count == 0)
            {
                return await ReportNonSuccessAndReturn("No claims in cache or returned from introspection endpoint.", Context, Scheme, Events, Options);
            }

            var isInActive = claims.Any(c =>
                string.Equals(c.Type, "active", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Value, "false", StringComparison.OrdinalIgnoreCase));
            if (isInActive)
            {
                return await ReportNonSuccessAndReturn("Token is not active.", Context, Scheme, Events, Options);
            }

            return await CreateTicket(claims, token, Context, Scheme, Events, Options);
        }
        catch (PreventCacheException pce)
        {
            Log.IntrospectionError(_logger, pce.Message, null);
            return await ReportNonSuccessAndReturn(pce.Message, Context, Scheme, Events, Options);
        }
        // catch (Exception ex)
        // {
        //     return await ReportNonSuccessAndReturn("Unhandled exception: " + ex.Message, Context, Scheme, Events, Options);
        // }
    }

    private static async Task<AuthenticateResult> ReportNonSuccessAndReturn(
        string error,
        HttpContext httpContext,
        AuthenticationScheme scheme,
        OAuth2IntrospectionEvents events,
        OAuth2IntrospectionOptions options)
    {
        var authenticationFailedContext = new AuthenticationFailedContext(httpContext, scheme, options, error);

        await events.AuthenticationFailed(authenticationFailedContext);

        return authenticationFailedContext.Result ?? AuthenticateResult.Fail(error);
    }

    private static async Task<TokenIntrospectionResponse> LoadClaimsForToken(
        string token,
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionEvents events,
        OAuth2IntrospectionOptions options)
    {
        var introspectionClient = await options.IntrospectionClient.Value.ConfigureAwait(false);
        using var request = await CreateTokenIntrospectionRequest(token, context, scheme, events, options);

        var requestSendingContext = new SendingRequestContext(context, scheme, options, request);

        await events.SendingRequest(requestSendingContext);

        return await introspectionClient.IntrospectTokenAsync(request).ConfigureAwait(false);
    }

    private static async ValueTask<TokenIntrospectionRequest> CreateTokenIntrospectionRequest(
        string token,
        HttpContext context,
        AuthenticationScheme scheme,
        OAuth2IntrospectionEvents events,
        OAuth2IntrospectionOptions options)
    {
        var clientAssertion = options.ClientAssertion ?? new ClientAssertion();
        if (options.ClientSecret == null && options.ClientAssertionExpirationTime <= DateTime.UtcNow)
        {
            await options.AssertionUpdateLock.WaitAsync();
            try
            {
                if (options.ClientAssertionExpirationTime <= DateTime.UtcNow)
                {
                    var updateClientAssertionContext =
                        new UpdateClientAssertionContext(context, scheme, options, clientAssertion);
                    await events.UpdateClientAssertion(updateClientAssertionContext);

                    options.ClientAssertion = updateClientAssertionContext.ClientAssertion;
                    options.ClientAssertionExpirationTime =
                        updateClientAssertionContext.ClientAssertionExpirationTime;
                }
            }
            finally
            {
                options.AssertionUpdateLock.Release();
            }
        }

        return new TokenIntrospectionRequest
        {
            Token = token,
            TokenTypeHint = options.TokenTypeHint,
            Address = options.IntrospectionEndpoint,
            ClientId = options.ClientId!,
            ClientSecret = options.ClientSecret,
            ClientAssertion = options.ClientAssertion!,
            ClientCredentialStyle = options.ClientCredentialStyle,
            AuthorizationHeaderStyle = options.AuthorizationHeaderStyle,
        };
    }

    private static async Task<AuthenticateResult> CreateTicket(
        IEnumerable<Claim> claims,
        string token,
        HttpContext httpContext,
        AuthenticationScheme scheme,
        OAuth2IntrospectionEvents events,
        OAuth2IntrospectionOptions options)
    {
        var authenticationType = options.AuthenticationType ?? scheme.Name;
        var id = new ClaimsIdentity(claims, authenticationType, options.NameClaimType, options.RoleClaimType);
        var principal = new ClaimsPrincipal(id);

        var tokenValidatedContext = new TokenValidatedContext(httpContext, scheme, options, token)
        {
            Principal = principal,
        };

        await events.TokenValidated(tokenValidatedContext);
        if (tokenValidatedContext.Result != null)
        {
            return tokenValidatedContext.Result;
        }

        if (options.SaveToken)
        {
            tokenValidatedContext.Properties.StoreTokens(new[]
            {
                new AuthenticationToken
                {
                    Name = "access_token",
                    Value = token
                }
            });
        }

        tokenValidatedContext.Success();
        return tokenValidatedContext.Result!;
    }

    /// <summary>
    /// Used to prevent caching of invalid introspection results
    /// such as errors or inactive tokens. Unfortunately hybrid cache
    /// has no built-in way to do this.
    /// </summary>
    /// <param name="errorMessage">Error message of why introspection result will not be cached</param>
    private class PreventCacheException(string? errorMessage) : Exception(errorMessage);
}
