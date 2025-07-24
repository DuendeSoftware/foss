// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using PublicApiGenerator;

namespace Duende.AccessTokenManagement;

public class PublicApiVerificationTests
{

    [Fact]
    public async Task VerifyPublicApi()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
        var publicApi = typeof(AccessTokenHandler).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }

    [Fact]
    public async Task VerifyPublicApi_OpenIdConnect()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
        var publicApi = typeof(OpenIdConnectUserAccessTokenHandler).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }
}
