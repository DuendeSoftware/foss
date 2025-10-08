// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

public static class ServiceProviderKeys
{
    /// <summary>
    /// Key for the introspection cache. Use this to inject a different cache implementation into the introspection handler.
    /// </summary>
    public const string IntrospectionCache = "IntrospectionCache";
}
