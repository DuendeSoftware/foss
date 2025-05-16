// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Allows transforming the principal before re-issuing the authentication session
/// </summary>
/// <param name="principal"></param>
/// <param name="ct"></param>
/// <returns></returns>

public delegate Task<ClaimsPrincipal> TransformPrincipalAfterRefreshAsync(ClaimsPrincipal principal, CT ct);
