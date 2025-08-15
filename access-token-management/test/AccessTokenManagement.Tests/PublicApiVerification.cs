// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Reflection;
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
            IncludeAssemblyAttributes = false,
        };
#pragma warning disable CS0618 // Type or member is obsolete
        var publicApi = typeof(AccessTokenRequestHandler).Assembly.GeneratePublicApi(apiGeneratorOptions);
#pragma warning restore CS0618 // Type or member is obsolete
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
#pragma warning disable CS0618 // Type or member is obsolete
        var publicApi = typeof(IOpenIdConnectUserTokenEndpoint).Assembly.GeneratePublicApi(apiGeneratorOptions);
#pragma warning restore CS0618 // Type or member is obsolete
        var settings = new VerifySettings();
        await Verify(publicApi, settings);
    }

    [Fact]
    public async Task GetAllPublicTypes()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var types = typeof(AccessTokenRequestHandler).Assembly.GetExportedTypes()
#pragma warning restore CS0618 // Type or member is obsolete
            .Where(t => t.IsPublic)
            .Select(t => FormatTypeName(t))
            .OrderBy(x => x); ;

        await Verify(string.Join(Environment.NewLine, types));
    }

    [Fact]
    public async Task GetAllPublicTypes_OpenIdConnect()
    {
        var types = typeof(IOpenIdConnectUserTokenEndpoint).Assembly.GetExportedTypes()
            .Where(t => t.IsPublic)
            .Select(t => FormatTypeName(t))
            .OrderBy(x => x);


        await Verify(string.Join(Environment.NewLine, types));
    }

    private static string FormatTypeName(Type t)
    {
        var obsolete = (t.GetCustomAttributes().Any(x => x is ObsoleteAttribute) ? " (obsolete)" : "");
        return $"{t.FullName}{obsolete}";
    }
}
