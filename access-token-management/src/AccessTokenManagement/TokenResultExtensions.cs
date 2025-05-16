// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public static class TokenResultExtensions
{
    /// <summary>
    /// Convenience method to extract the token from an asynchronous TokenResult.
    /// You can now do: GetTokenAsync(ct).GetToken().
    /// Note, this method will throw an InvalidOperationException with the token failure
    /// if the token result was not successful.
    /// </summary>
    /// <typeparam name="T">Token</typeparam>
    /// <param name="task">The task that retrieved the token. </param>
    /// <returns>Token if successful</returns>
    /// <exception cref="InvalidOperationException">Thrown if the token was not retrieved successfully. </exception>
    public static async Task<T> GetToken<T>(this Task<TokenResult<T>> task) where T : class
    {
        var result = await task;

        if (!result.WasSuccessful(out var token, out var failure))
        {
            throw new InvalidOperationException($"Failed to get token: {failure}");
        }

        return token;
    }
}
