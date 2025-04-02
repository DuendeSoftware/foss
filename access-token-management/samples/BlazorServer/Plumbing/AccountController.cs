// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorServer.Plumbing;

[AllowAnonymous]
public class AccountController : ControllerBase
{
    public IActionResult LogIn(string? returnUrl)
    {
        var redirectUri = "/";

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                redirectUri = returnUrl;
            }
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return Challenge(props);
    }

    public IActionResult LogOut()
    {
        return SignOut("cookie", "oidc");
    }
}
