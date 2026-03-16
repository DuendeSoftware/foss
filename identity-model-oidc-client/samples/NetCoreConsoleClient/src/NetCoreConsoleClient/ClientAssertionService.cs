// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ConsoleClientWithBrowser;

/// <summary>
/// Creates signed client assertion JWTs (RFC 7523 / private_key_jwt).
/// Each call to <see cref="CreateAssertionAsync"/> produces a JWT with a fresh
/// <c>jti</c> and <c>iat</c>, which is critical when retries (e.g. DPoP nonce
/// challenges) require a new assertion to avoid replay rejection.
/// </summary>
public class ClientAssertionService
{
    private readonly string _clientId;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;

    public ClientAssertionService(string clientId, string audience, SigningCredentials signingCredentials)
    {
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _audience = audience ?? throw new ArgumentNullException(nameof(audience));
        _signingCredentials = signingCredentials ?? throw new ArgumentNullException(nameof(signingCredentials));
    }

    /// <summary>
    /// Creates a fresh <see cref="ClientAssertion"/> with a unique <c>jti</c>.
    /// </summary>
    public Task<ClientAssertion> CreateAssertionAsync()
    {
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _clientId,
            Audience = _audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(1),
            SigningCredentials = _signingCredentials,
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { "typ", "client-authentication+jwt" }
            },
            Claims = new Dictionary<string, object>
            {
                { JwtClaimTypes.JwtId, Guid.NewGuid().ToString() },
                { JwtClaimTypes.Subject, _clientId },
            }
        };

        var handler = new JsonWebTokenHandler();
        var jwt = handler.CreateToken(descriptor);

        return Task.FromResult(new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = jwt
        });
    }

    /// <summary>
    /// Creates a new RSA signing credential suitable for client assertion signing.
    /// </summary>
    public static SigningCredentials CreateSigningCredentials()
    {
        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa);
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }
}
