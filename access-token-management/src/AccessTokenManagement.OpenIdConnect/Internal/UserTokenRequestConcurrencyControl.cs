// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Duende.AccessTokenManagement.OpenIdConnect.Internal;

/// <summary>
/// Default implementation for token request concurrency control.
/// If multiple requests ask for the same token to be refreshed (the same refresh token and the same
/// parameters), only one will execute. The rest will just wait for this one request.
/// Requests for different parameters, such as a different resource, each execute.
/// </summary>
internal class UserTokenRequestConcurrencyControl : IUserTokenRequestConcurrencyControl
{
    // this is what provides the synchronization; assumes this service is a singleton in DI.
    ConcurrentDictionary<string, Lazy<Task<TokenResult<UserToken>>>> Dictionary { get; } = new();

    /// <inheritdoc/>
    public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key, Func<Task<TokenResult<UserToken>>> tokenRetriever, CT ct = default) =>
        ExecuteWithConcurrencyControlAsync(key, new UserTokenRequestParameters(), tokenRetriever, ct);

    /// <inheritdoc/>
    public async Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
        UserRefreshToken key,
        UserTokenRequestParameters parameters,
        Func<Task<TokenResult<UserToken>>> tokenRetriever,
        CT ct = default)
    {
        var requestKey = BuildRequestKey(key, parameters);
        var request = Dictionary.GetOrAdd(requestKey, _ => new Lazy<Task<TokenResult<UserToken>>>(tokenRetriever));
        try
        {
            return await request.Value.ConfigureAwait(false);
        }
        finally
        {
            // Only remove the entry we awaited, not one a later request may have added in the meantime.
            Dictionary.TryRemove(new KeyValuePair<string, Lazy<Task<TokenResult<UserToken>>>>(requestKey, request));
        }
    }

    /// <summary>
    /// Builds a key that is equal for requests that would result in the same token request: everything that is
    /// sent to the token endpoint and can change the token that comes back.
    /// </summary>
    private static string BuildRequestKey(UserRefreshToken refreshToken, UserTokenRequestParameters parameters)
    {
        var sb = new StringBuilder();
        Append(sb, refreshToken.RefreshToken.ToString());

        // The token is bound to this key. Hashed, so the key doesn't hold the private key material.
        Append(sb, refreshToken.DPoPProofKey is { } dPoPKey
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dPoPKey.ToString())))
            : null);

        Append(sb, parameters.Resource?.ToString());
        Append(sb, parameters.Scope?.ToString());
        Append(sb, parameters.ChallengeScheme?.ToString());
        Append(sb, parameters.SignInScheme?.ToString());

        // A caller-supplied assertion is sent as the client credential. Assertions created by
        // IClientAssertionService aren't in the parameters, so they don't prevent grouping.
        Append(sb, parameters.Assertion?.Type);
        Append(sb, parameters.Assertion?.Value);

        foreach (var parameter in parameters.Parameters)
        {
            Append(sb, parameter.Key);
            Append(sb, parameter.Value);
        }

        return sb.ToString();

        // Length-prefix every value, so no combination of values can produce the same key as another.
        static void Append(StringBuilder sb, string? value)
        {
            if (value == null)
            {
                sb.Append("-|");
            }
            else
            {
                sb.Append(value.Length).Append(':').Append(value).Append('|');
            }
        }
    }
}
