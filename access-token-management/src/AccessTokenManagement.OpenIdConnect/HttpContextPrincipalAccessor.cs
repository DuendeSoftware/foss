// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Accesses the current principal based on the HttpContext.User.
/// </summary>
[Obsolete("This type is going to be removed in a future release.")]
public class HttpContextUserAccessor : IUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// ctor
    /// </summary>
    public HttpContextUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> GetCurrentUserAsync()
    {
        return Task.FromResult(_httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal());
    }
}
