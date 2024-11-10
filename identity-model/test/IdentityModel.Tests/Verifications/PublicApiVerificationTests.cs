// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using PublicApiGenerator;

namespace Duende.IdentityModel.Verifications;

public class PublicApiVerificationTests
{
    [Fact]
    public async Task VerifyPublicApi()
    {
        var apiGeneratorOptions = new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
        };
        var publicApi = typeof(JwtClaimTypes).Assembly.GeneratePublicApi(apiGeneratorOptions);
        var settings = new VerifySettings();
        settings.UniqueForTargetFrameworkAndVersion();
        await Verify(publicApi, settings);
    }
}