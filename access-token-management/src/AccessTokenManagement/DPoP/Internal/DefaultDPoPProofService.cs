// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.AccessTokenManagement.Internal;
using Duende.IdentityModel;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.DPoP.Internal;

/// <summary>
/// Default implementation of IDPoPProofService
/// </summary>
internal class DefaultDPoPProofService(IDPoPNonceStore dPoPNonceStore) : IDPoPProofService
{
    /// <inheritdoc/>
    public async Task<DPoPProofString?> CreateProofTokenAsync(
        DPoPProof request,
        CT ct)
    {
        var jsonWebKey = new JsonWebKey(request.ProofKey);

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
            { JwtClaimTypes.DPoPHttpMethod, request.Method.ToString().ToUpper() },
            { JwtClaimTypes.DPoPHttpUrl, request.Url.ToString() },
            { JwtClaimTypes.IssuedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
        };

        if (request.AccessToken != null)
        {
            // ath: hash of the access token. The value MUST be the result of a base64url encoding
            // the SHA-256 hash of the ASCII encoding of the associated access token's value.
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(request.AccessToken.Value.ToString()));
            var ath = Base64Url.Encode(hash);

            payload.Add(JwtClaimTypes.DPoPAccessTokenHash, ath);
        }

        var nonce = request.DPoPNonce;
        var dPoPNonceContext = new DPoPNonceContext
        {
            Url = request.Url,
            Method = request.Method,
        };
        if (nonce == null)
        {
            nonce = await dPoPNonceStore.GetNonceAsync(dPoPNonceContext, ct);
        }
        else
        {
            await dPoPNonceStore.StoreNonceAsync(dPoPNonceContext, nonce.Value, ct);
        }

        if (nonce != null)
        {
            payload.Add(JwtClaimTypes.Nonce, nonce.Value.ToString());
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
        var proofToken = handler.CreateToken(JsonSerializer.Serialize(payload, DuendeAccessTokenSerializationContext.Default.DictionaryStringObject), key, header);

        return DPoPProofString.Parse(proofToken);
    }

    /// <inheritdoc/>
    public DPoPProofThumbprint? GetProofKeyThumbprint(ProofKeyString keyString) =>
        DPoPProofThumbprint.FromJsonWebKey(new JsonWebKey(keyString));
}
