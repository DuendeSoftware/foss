// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using PublicApiGenerator;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public class PublicApiVerification
{
    [Fact]
    public async Task VerifyPublicApi()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false
        };
        var publicApi = typeof(OAuth2IntrospectionHandler).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }
}
