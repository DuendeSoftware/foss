// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;

public class ClaimLite
{
    public string Type { get; set; }
    public string Value { get; set; }
}
