// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.Types;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Additional optional per request parameters for a user access token request
/// </summary>
public record UserTokenRequestParameters : TokenRequestParameters
{
    /// <summary>
    /// Overrides the default sign-in scheme. This information may be used for state management.
    /// </summary>
    public Scheme? SignInScheme { get; set; }

    /// <summary>
    /// Overrides the default challenge scheme. This information may be used for deriving token service configuration.
    /// </summary>
    public Scheme? ChallengeScheme { get; set; }
}
