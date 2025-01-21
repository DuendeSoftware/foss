// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace Duende.IdentityModel.Client;

/// <summary>
/// Models an OAuth 2.0 introspection response as defined by <a href="https://datatracker.ietf.org/doc/html/rfc7662">RFC 7662 - OAuth 2.0 Token Introspection</a>
/// </summary>
/// <seealso cref="ProtocolResponse" />
public class TokenIntrospectionResponse : ProtocolResponse
{
    /// <summary>
    /// Allows to initialize instance specific data.
    /// </summary>
    /// <param name="initializationData">The initialization data.</param>
    /// <returns></returns>
    protected override Task InitializeAsync(object? initializationData = null)
    {
        if (!IsError)
        {
            if(Json == null)
            {
                throw new InvalidOperationException("Json is null"); // TODO better exception
            }
            var issuer = Json?.TryGetString("iss");
            var claims = Json?.ToClaims(issuer, "scope").ToList() ?? new List<Claim>();

            // due to a bug in identityserver - we need to be able to deal with the scope list both in array as well as space-separated list format
            var scope = Json?.TryGetValue("scope");

            // scope element exists
            // if (scope != null)
            // {
            // it's an array
            if (scope?.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in scope?.EnumerateArray() ?? Enumerable.Empty<JsonElement>())
                {
                    claims.Add(new Claim("scope", item.ToString(), ClaimValueTypes.String, issuer));
                }
            }
            else
            {
                // it's a string
                var scopeString = scope.ToString() ?? "";

                var scopes = scopeString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var scopeValue in scopes)
                {
                    claims.Add(new Claim("scope", scopeValue, ClaimValueTypes.String, issuer));
                }
            }
            // }

            Scopes = claims.Where(c => c.Type == JwtClaimTypes.Scope).Select(c => c.Value).ToArray();
            ClientId = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.ClientId)?.Value;
            UserName = claims.FirstOrDefault(c => c.Type == "username")?.Value;
            TokenType = claims.FirstOrDefault(c => c.Type == "token_type")?.Value;
            Expiration = GetTime(claims, JwtClaimTypes.Expiration);
            IssuedAt = GetTime(claims, JwtClaimTypes.IssuedAt);
            NotBefore = GetTime(claims, JwtClaimTypes.NotBefore);
            Subject = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject)?.Value;
            Audiences = claims.Where(c => c.Type == JwtClaimTypes.Audience).Select(c => c.Value).ToArray();
            Issuer = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Issuer)?.Value;
            JwtId = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.JwtId)?.Value;

            Claims = claims;
        }
        
        return Task.CompletedTask;
    }

    private static DateTimeOffset? GetTime(List<Claim> claims, string claimType)
    {
        var claimValue = claims.FirstOrDefault(e => e.Type == claimType)?.Value;
        if (claimValue == null) return null;

        var seconds = long.Parse(claimValue, NumberStyles.AllowLeadingSign, NumberFormatInfo.InvariantInfo);
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    /// <summary>
    /// Gets a value indicating whether the token is active.
    /// </summary>
    /// <value>
    ///   <c>true</c> if the token is active; otherwise, <c>false</c>.
    /// </value>
    public bool IsActive => Json?.TryGetBoolean("active") ?? false;

    /// <summary>
    /// Gets the list of scopes associated to the token.
    /// </summary>
    /// <value>
    /// The list of scopes associated to the token or an empty array if no <c>scope</c> claim is present.
    /// </value>
    public string[] Scopes { get; private set; } = [];

    /// <summary>
    /// Gets the client identifier for the OAuth 2.0 client that requested the token.
    /// </summary>
    /// <value>
    /// The client identifier for the OAuth 2.0 client that requested the token or null if the <c>client_id</c> claim is missing.
    /// </value>
    public string? ClientId { get; private set; }

    /// <summary>
    /// Gets the human-readable identifier for the resource owner who authorized the token.
    /// </summary>
    /// <value>
    /// The human-readable identifier for the resource owner who authorized the token or null if the <c>username</c> claim is missing.
    /// </value>
    public string? UserName { get; private set; }

    /// <summary>
    /// Gets the type of the token as defined in <a href="https://datatracker.ietf.org/doc/html/rfc6749#section-5.1">section 5.1 of OAuth 2.0 (RFC6749)</a>.
    /// </summary>
    /// <value>
    /// The type of the token as defined in <a href="https://datatracker.ietf.org/doc/html/rfc6749#section-5.1">section 5.1 of OAuth 2.0 (RFC6749)</a> or null if the <c>token_type</c> claim is missing.
    /// </value>
    public string? TokenType { get; private set; }

    /// <summary>
    /// Gets the time on or after which the token must not be accepted for processing.
    /// </summary>
    /// <value>
    /// The expiration time of the token or null if the <c>exp</c> claim is missing.
    /// </value>
    public DateTimeOffset? Expiration { get; private set; }

    /// <summary>
    /// Gets the time when the token was issued.
    /// </summary>
    /// <value>
    /// The issuance time of the token or null if the <c>iat</c> claim is missing.
    /// </value>
    public DateTimeOffset? IssuedAt { get; private set; }

    /// <summary>
    /// Gets the time before which the token must not be accepted for processing.
    /// </summary>
    /// <value>
    /// The validity start time of the token or null if the <c>nbf</c> claim is missing.
    /// </value>
    public DateTimeOffset? NotBefore { get; private set; }

    /// <summary>
    /// Gets the subject of the token. Usually a machine-readable identifier of the resource owner who authorized the token.
    /// </summary>
    /// <value>
    /// The subject of the token or null if the <c>sub</c> claim is missing.
    /// </value>
    public string? Subject { get; private set; }

    /// <summary>
    /// Gets the service-specific list of string identifiers representing the intended audience for the token.
    /// </summary>
    /// <value>
    /// The service-specific list of string identifiers representing the intended audience for the token or an empty array if no <c>aud</c> claim is present.
    /// </value>
    public string[] Audiences { get; private set; } = [];

    /// <summary>
    /// Gets the string representing the issuer of the token.
    /// </summary>
    /// <value>
    /// The string representing the issuer of the token or null if the <c>iss</c> claim is missing.
    /// </value>
    public string? Issuer { get; private set; }

    /// <summary>
    /// Gets the string identifier for the token.
    /// </summary>
    /// <value>
    /// The string identifier for the token or null if the <c>jti</c> claim is missing.
    /// </value>
    public string? JwtId { get; private set; }

    /// <summary>
    /// Gets the claims.
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public IEnumerable<Claim> Claims { get; protected set; } = Enumerable.Empty<Claim>();

}