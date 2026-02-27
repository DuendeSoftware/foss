// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.Framework;

internal sealed class JwtClientAssertionService(
    string clientId,
    SigningCredentials credentials) : IClientAssertionService
{
    public Task<ClientAssertion?> GetClientAssertionAsync(
        ClientCredentialsClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CancellationToken ct = default)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = clientId,
            Audience = "https://identityserver/connect/token",
            Expires = DateTime.UtcNow.AddMinutes(1),
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                { JwtClaimTypes.JwtId, Guid.NewGuid().ToString() },
                { JwtClaimTypes.Subject, clientId },
                { JwtClaimTypes.IssuedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            }
        };

        var jwt = new JsonWebTokenHandler().CreateToken(descriptor);

        return Task.FromResult<ClientAssertion?>(new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = jwt
        });
    }
}
