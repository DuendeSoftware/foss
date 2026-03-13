// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WebClientAssertions;

/// <summary>
/// Creates signed client assertion JWTs (RFC 7523 / private_key_jwt) for use
/// with Duende's Access Token Management library.
///
/// Each call produces a JWT with a fresh <c>jti</c> and <c>iat</c>, which is
/// critical when DPoP nonce retries require a new assertion to avoid replay
/// rejection by the authorization server.
/// </summary>
public class ClientAssertionService : IClientAssertionService
{
    /// <summary>
    /// RSA private key that matches the public key registered at
    /// demo.duendesoftware.com for the JWT client authentication clients.
    /// In production, load from a secure store (e.g. Azure Key Vault).
    /// </summary>
    private static readonly SigningCredentials Credential = new(
        new JsonWebKey("""
            {
                "d":"GmiaucNIzdvsEzGjZjd43SDToy1pz-Ph-shsOUXXh-dsYNGftITGerp8bO1iryXh_zUEo8oDK3r1y4klTonQ6bLsWw4ogjLPmL3yiqsoSjJa1G2Ymh_RY_sFZLLXAcrmpbzdWIAkgkHSZTaliL6g57vA7gxvd8L4s82wgGer_JmURI0ECbaCg98JVS0Srtf9GeTRHoX4foLWKc1Vq6NHthzqRMLZe-aRBNU9IMvXNd7kCcIbHCM3GTD_8cFj135nBPP2HOgC_ZXI1txsEf-djqJj8W5vaM7ViKU28IDv1gZGH3CatoysYx6jv1XJVvb2PH8RbFKbJmeyUm3Wvo-rgQ",
                "dp":"YNjVBTCIwZD65WCht5ve06vnBLP_Po1NtL_4lkholmPzJ5jbLYBU8f5foNp8DVJBdFQW7wcLmx85-NC5Pl1ZeyA-Ecbw4fDraa5Z4wUKlF0LT6VV79rfOF19y8kwf6MigyrDqMLcH_CRnRGg5NfDsijlZXffINGuxg6wWzhiqqE",
                "dq":"LfMDQbvTFNngkZjKkN2CBh5_MBG6Yrmfy4kWA8IC2HQqID5FtreiY2MTAwoDcoINfh3S5CItpuq94tlB2t-VUv8wunhbngHiB5xUprwGAAnwJ3DL39D2m43i_3YP-UO1TgZQUAOh7Jrd4foatpatTvBtY3F1DrCrUKE5Kkn770M",
                "e":"AQAB",
                "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                "kty":"RSA",
                "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw",
                "p":"7enorp9Pm9XSHaCvQyENcvdU99WCPbnp8vc0KnY_0g9UdX4ZDH07JwKu6DQEwfmUA1qspC-e_KFWTl3x0-I2eJRnHjLOoLrTjrVSBRhBMGEH5PvtZTTThnIY2LReH-6EhceGvcsJ_MhNDUEZLykiH1OnKhmRuvSdhi8oiETqtPE",
                "q":"0CBLGi_kRPLqI8yfVkpBbA9zkCAshgrWWn9hsq6a7Zl2LcLaLBRUxH0q1jWnXgeJh9o5v8sYGXwhbrmuypw7kJ0uA3OgEzSsNvX5Ay3R9sNel-3Mqm8Me5OfWWvmTEBOci8RwHstdR-7b9ZT13jk-dsZI7OlV_uBja1ny9Nz9ts",
                "qi":"pG6J4dcUDrDndMxa-ee1yG4KjZqqyCQcmPAfqklI2LmnpRIjcK78scclvpboI3JQyg6RCEKVMwAhVtQM6cBcIO3JrHgqeYDblp5wXHjto70HVW6Z8kBruNx1AH9E8LzNvSRL-JVTFzBkJuNgzKQfD0G77tQRgJ-Ri7qu3_9o1M4"
            }
        """),
        SecurityAlgorithms.RsaSha256);

    private const string Authority = "https://demo.duendesoftware.com";

    public Task<ClientAssertion?> GetClientAssertionAsync(
        ClientCredentialsClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CancellationToken ct = default)
    {
        // Determine the client_id for the assertion's issuer/subject claims.
        // The library calls this with different clientName values depending on context:
        //   - scheme-based name during OIDC flows (code exchange, refresh)
        //   - the literal client name "m2m.jwt" for the named M2M client
        var clientId = ResolveClientId(clientName);

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = clientId,
            Audience = Authority,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(1),
            SigningCredentials = Credential,

            Claims = new Dictionary<string, object>
            {
                { JwtClaimTypes.JwtId, Guid.NewGuid().ToString() },
                { JwtClaimTypes.Subject, clientId },
            },

            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { "typ", "client-authentication+jwt" }
            }
        };

        var handler = new JsonWebTokenHandler();
        var jwt = handler.CreateToken(descriptor);

        return Task.FromResult<ClientAssertion?>(new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = jwt
        });
    }

    /// <summary>
    /// Maps the ATM client name to the actual OAuth client_id used in the assertion.
    /// </summary>
    private static string ResolveClientId(ClientCredentialsClientName? clientName)
    {
        var name = clientName?.ToString();

        // Default / OIDC scheme-based client → use the interactive DPoP client
        if (string.IsNullOrEmpty(name) || name.Contains(OpenIdConnectTokenManagementDefaults.ClientCredentialsClientNamePrefix))
        {
            return "interactive.confidential.jwt.dpop";
        }

        // Named M2M client
        return name switch
        {
            "m2m.jwt" => "m2m.jwt",
            _ => name
        };
    }
}
