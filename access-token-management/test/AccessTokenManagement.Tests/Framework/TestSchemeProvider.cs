// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Duende.AccessTokenManagement.Framework;

public class TestSchemeProvider(string signInSchemeName = "testScheme") : IAuthenticationSchemeProvider
{
    private readonly AuthenticationScheme? _defaultSignInScheme
        = new(signInSchemeName, signInSchemeName, typeof(CookieAuthenticationHandler));

    public Task<AuthenticationScheme?> GetDefaultSignInSchemeAsync() => Task.FromResult(_defaultSignInScheme);

    #region Not Implemented (No tests have needed these yet)

    public void AddScheme(AuthenticationScheme scheme) => throw new NotImplementedException();

    public Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync() => throw new NotImplementedException();

    public Task<AuthenticationScheme?> GetDefaultAuthenticateSchemeAsync() => throw new NotImplementedException();

    public Task<AuthenticationScheme?> GetDefaultChallengeSchemeAsync() => throw new NotImplementedException();

    public Task<AuthenticationScheme?> GetDefaultForbidSchemeAsync() => throw new NotImplementedException();


    public Task<AuthenticationScheme?> GetDefaultSignOutSchemeAsync() => throw new NotImplementedException();

    public Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() => throw new NotImplementedException();

    public Task<AuthenticationScheme?> GetSchemeAsync(string name) => throw new NotImplementedException();

    public void RemoveScheme(string name) => throw new NotImplementedException();

    #endregion
}
