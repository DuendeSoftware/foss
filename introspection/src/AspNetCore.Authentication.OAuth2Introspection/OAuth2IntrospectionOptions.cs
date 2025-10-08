// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

/// <summary>
/// Options class for the OAuth 2.0 introspection endpoint authentication handler
/// </summary>
public class OAuth2IntrospectionOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// ctor
    /// </summary>
    public OAuth2IntrospectionOptions() => Events = new OAuth2IntrospectionEvents();

    /// <summary>
    /// Sets the base-path of the token provider.
    /// If set, the OpenID Connect discovery document will be used to find the introspection endpoint.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Sets the URL of the introspection endpoint.
    /// If set, Authority is ignored.
    /// </summary>
    public string? IntrospectionEndpoint { get; set; }

    /// <summary>
    /// Specifies the id of the introspection client (required).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Specifies the shared secret of the introspection client.
    /// </summary>
    public string? ClientSecret { get; set; }

    internal readonly SemaphoreSlim AssertionUpdateLock = new(1, 1);

    internal ClientAssertion? ClientAssertion { get; set; }

    internal DateTime ClientAssertionExpirationTime { get; set; }

    /// <summary>
    /// Specifies how client id and secret are being sent
    /// </summary>
    public ClientCredentialStyle ClientCredentialStyle { get; set; } = ClientCredentialStyle.PostBody;

    /// <summary>
    /// Specifies how the authorization header gets formatted (if used)
    /// </summary>
    public BasicAuthenticationHeaderStyle AuthorizationHeaderStyle { get; set; } = BasicAuthenticationHeaderStyle.Rfc2617;

    /// <summary>
    /// Specifies the token type hint of the introspection client.
    /// </summary>
    public string TokenTypeHint { get; set; } = OidcConstants.TokenTypes.AccessToken;

    /// <summary>
    /// Specifies the claim type to use for the name claim (defaults to 'name')
    /// </summary>
    public string NameClaimType { get; set; } = "name";

    /// <summary>
    /// Specifies the claim type to use for the role claim (defaults to 'role')
    /// </summary>
    public string RoleClaimType { get; set; } = "role";

    /// <summary>
    /// Specifies the authentication type to use for the authenticated identity.
    /// If not set, the authentication scheme name is used as the authentication
    /// type (defaults to null).
    /// </summary>
    public string? AuthenticationType { get; set; }

    /// <summary>
    /// Specifies the policy for the discovery client
    /// </summary>
    public DiscoveryPolicy DiscoveryPolicy { get; set; } = new();

    /// <summary>
    /// Specifies whether tokens that contain dots (most likely a JWT) are skipped
    /// </summary>
    public bool SkipTokensWithDots { get; set; } = false;

    /// <summary>
    /// Specifies whether the token should be stored in the context, and thus be available for the duration of the request
    /// </summary>
    public bool SaveToken { get; set; } = true;

    /// <summary>
    /// Specifies for how long the outcome of the token validation should be cached.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Specifies the flags controlling the behavior of the hybrid cache when setting the cache entry.
    /// The default is <see cref="HybridCacheEntryFlags.None"/>, which means that both the local and distributed
    /// caches will be written to when setting the cache entry. If you want to disable writing to either cache, or both
    /// if you wish to disable caching entirely, you can set the appropriate flag(s).
    /// </summary>
    public HybridCacheEntryFlags SetCacheEntryFlags { get; set; } = HybridCacheEntryFlags.None;

    /// <summary>
    /// Specifies the prefix of the cache key (token).
    /// </summary>
    public string CacheKeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Specifies the method how to generate the cache key from the token
    /// </summary>
    public Func<OAuth2IntrospectionOptions, string, string> CacheKeyGenerator { get; set; } = CacheUtils.CacheKeyFromToken();

    /// <summary>
    /// Specifies the method how to retrieve the token from the HTTP request
    /// </summary>
    public Func<HttpRequest, string?> TokenRetriever { get; set; } = TokenRetrieval.FromAuthorizationHeader();

    /// <summary>
    /// Gets or sets the <see cref="OAuth2IntrospectionEvents"/> used to handle authentication events.
    /// </summary>
    public new OAuth2IntrospectionEvents Events
    {
        get => (OAuth2IntrospectionEvents)base.Events!;
        set => base.Events = value;
    }

    internal AsyncLazy<HttpClient> IntrospectionClient { get; set; } = null!;

    /// <summary>
    /// Check that the options are valid. Should throw an exception if things are not ok.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// You must either set Authority or IntrospectionEndpoint
    /// or
    /// You must either set a ClientId or set an introspection HTTP handler
    /// </exception>
    /// <exception cref="ArgumentException">TokenRetriever must be set - TokenRetriever</exception>
    public override void Validate()
    {
        base.Validate();

        if (Authority.IsMissing() && IntrospectionEndpoint.IsMissing())
        {
            throw new InvalidOperationException("You must either set Authority or IntrospectionEndpoint");
        }
    }
}
