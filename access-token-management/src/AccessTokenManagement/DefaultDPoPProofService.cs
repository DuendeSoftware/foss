// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement;

/// <summary>
/// Default implementation of IDPoPProofService
/// </summary>
[Obsolete(Constants.AtmPublicSurfaceInternal, UrlFormat = Constants.AtmPublicSurfaceLink)]
public class DefaultDPoPProofService(IDPoPNonceStore dPoPNonceStore, ILogger<DefaultDPoPProofService> logger) : IDPoPProofService
{
    /// <inheritdoc/>
    public virtual async Task<DPoPProof?> CreateProofTokenAsync(DPoPProofRequest request)
    {
        JsonWebKey jsonWebKey;

        try
        {
            jsonWebKey = new JsonWebKey(request.DPoPJsonWebKey);
        }
        catch (Exception ex)
        {
            logger.FailedToParseJsonWebKey(ex);
            return null;
        }

        // jwk: representing the public key chosen by the client, in JSON Web Key (JWK) [RFC7517] format,
        // as defined in Section 4.1.3 of [RFC7515]. MUST NOT contain a private key.
        Dictionary<string, string> jwk;
        if (string.Equals(jsonWebKey.Kty, JsonWebAlgorithmsKeyTypes.EllipticCurve))
        {
            jwk = new()
            {
                { "kty", jsonWebKey.Kty },
                { "x", jsonWebKey.X },
                { "y", jsonWebKey.Y },
                { "crv", jsonWebKey.Crv }
            };
        }
        else if (string.Equals(jsonWebKey.Kty, JsonWebAlgorithmsKeyTypes.RSA))
        {
            jwk = new()
            {
                { "kty", jsonWebKey.Kty },
                { "e", jsonWebKey.E },
                { "n", jsonWebKey.N }
            };
        }
        else
        {
            throw new InvalidOperationException("invalid key type: " + jsonWebKey.Kty);
        }

        var header = new Dictionary<string, object>()
        {
            //{ "alg", "RS265" }, // JsonWebTokenHandler requires adding this itself
            { "typ", JwtClaimTypes.JwtTypes.DPoPProofToken },
            { JwtClaimTypes.JsonWebKey, jwk },
        };

        var payload = new Dictionary<string, object>
        {
            { JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId() },
            { JwtClaimTypes.DPoPHttpMethod, request.Method },
            { JwtClaimTypes.DPoPHttpUrl, request.Url },
            { JwtClaimTypes.IssuedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
        };

        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            // ath: hash of the access token. The value MUST be the result of a base64url encoding 
            // the SHA-256 hash of the ASCII encoding of the associated access token's value.
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(request.AccessToken));
            var ath = Base64Url.Encode(hash);

            payload.Add(JwtClaimTypes.DPoPAccessTokenHash, ath);
        }

        var nonce = request.DPoPNonce;
        if (string.IsNullOrEmpty(nonce))
        {
            nonce = await dPoPNonceStore.GetNonceAsync(new DPoPNonceContext
            {
                Url = request.Url,
                Method = request.Method,
            });
        }
        else
        {
            await dPoPNonceStore.StoreNonceAsync(new DPoPNonceContext
            {
                Url = request.Url,
                Method = request.Method,
            }, nonce);
        }

        if (!string.IsNullOrEmpty(nonce))
        {
            payload.Add(JwtClaimTypes.Nonce, nonce);
        }

        if (request.AdditionalPayloadClaims?.Count > 0)
        {
            foreach (var claim in request.AdditionalPayloadClaims)
            {
                payload.Add(claim.Key, claim.Value);
            }
        }

        var handler = new JsonWebTokenHandler() { SetDefaultTimesOnTokenCreation = false };
        var key = new SigningCredentials(jsonWebKey, jsonWebKey.Alg);
        var proofToken = handler.CreateToken(JsonSerializer.Serialize(payload, Implementation.DuendeAccessTokenSerializationContext.Default.DictionaryStringObject), key, header);

        return new DPoPProof { ProofToken = proofToken! };
    }

    /// <inheritdoc/>
    public virtual string? GetProofKeyThumbprint(DPoPProofRequest request)
    {
        try
        {
            var jsonWebKey = new JsonWebKey(request.DPoPJsonWebKey);
            return Base64UrlEncoder.Encode(jsonWebKey.ComputeJwkThumbprint());
        }
        catch (Exception ex)
        {
            logger.FailedToCreateThumbprintFromJsonWebKey(ex);
        }
        return null;
    }
}
