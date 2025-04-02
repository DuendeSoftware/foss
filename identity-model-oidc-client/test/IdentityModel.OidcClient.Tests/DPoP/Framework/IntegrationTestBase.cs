// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.IdentityModel.OidcClient.DPoP.Framework;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly IdentityServerHost IdentityServerHost;
    protected ApiHost ApiHost;

    public IntegrationTestBase()
    {
        IdentityServerHost = new IdentityServerHost();
        ApiHost = new ApiHost(IdentityServerHost);
    }

    public async ValueTask DisposeAsync()
    {
        await ApiHost.DisposeAsync();
        await IdentityServerHost.DisposeAsync();
    }

    public async ValueTask InitializeAsync()
    {
        await ApiHost.InitializeAsync();
        await IdentityServerHost.InitializeAsync();
    }
}
