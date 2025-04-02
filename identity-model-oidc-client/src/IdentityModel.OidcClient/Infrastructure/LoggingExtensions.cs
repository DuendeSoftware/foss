// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityModel.OidcClient.Infrastructure;

internal static class LoggingExtensions
{
    [DebuggerStepThrough]
    public static void LogClaims(this ILogger logger, IEnumerable<Claim> claims)
    {
        foreach (var claim in claims)
        {
            logger.LogDebug($"Claim: {claim.Type}: {claim.Value}");
        }
    }

    [DebuggerStepThrough]
    public static void LogClaims(this ILogger logger, ClaimsPrincipal user)
    {
        logger.LogClaims(user.Claims);
    }
}
