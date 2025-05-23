// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace WebJarJwt;

public class ClientAssertionService : IClientAssertionService
{
    private readonly IOpenIdConnectConfigurationService _configurationService;

    private static string RsaKey =
        """
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
        """;

    private static SigningCredentials Credential = new(new JsonWebKey(RsaKey), "RS256");

    public ClientAssertionService(
        IOpenIdConnectConfigurationService configurationService) => _configurationService = configurationService;

    public async Task<ClientAssertion?> GetClientAssertionAsync(ClientCredentialsClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CancellationToken ct = default)
    {
        var config = await _configurationService.GetOpenIdConnectConfigurationAsync(ct: ct);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config.ClientId.ToString(),
            Audience = config.TokenEndpoint.GetLeftPart(UriPartial.Authority),
            Expires = DateTime.UtcNow.AddMinutes(1),
            SigningCredentials = Credential,

            Claims = new Dictionary<string, object>
            {
                { JwtClaimTypes.JwtId, Guid.NewGuid().ToString() },
                { JwtClaimTypes.Subject, config.ClientId.ToString()! },
                { JwtClaimTypes.IssuedAt, DateTime.UtcNow.ToEpochTime() }
            },

            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                { JwtClaimTypes.TokenType, "client-authentication+jwt" }
            }
        };

        var handler = new JsonWebTokenHandler();
        var jwt = handler.CreateToken(descriptor);

        return new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = jwt
        };
    }

    public async Task<string> SignAuthorizeRequest(OpenIdConnectMessage message,
        CancellationToken ct = default)
    {
        var config = await _configurationService.GetOpenIdConnectConfigurationAsync();

        var parameters = new Dictionary<string, object>();
        foreach (var parameter in message.Parameters)
        {
            parameters.Add(parameter.Key, parameter.Value);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config.ClientId.ToString(),
            Audience = config.TokenEndpoint.GetLeftPart(UriPartial.Authority),
            Expires = DateTime.UtcNow.AddMinutes(1),
            SigningCredentials = Credential,

            Claims = parameters
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }
}
