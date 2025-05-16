// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel;

namespace Duende.AccessTokenManagement.DPoP;

internal static class DPoPErrors
{
    private static readonly string[] DpopErrors =
    [
        OidcConstants.TokenErrors.UseDPoPNonce,
        OidcConstants.TokenErrors.InvalidDPoPProof
    ];

    public static bool IsDPoPError(string? message)
    {
        if (message == null)
        {
            return false;
        }

        return DpopErrors.Contains(message);
    }
}
