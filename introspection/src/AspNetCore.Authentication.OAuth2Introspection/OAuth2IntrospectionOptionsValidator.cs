// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.OAuth2Introspection;

internal class OAuth2IntrospectionOptionsValidator : IValidateOptions<OAuth2IntrospectionOptions>
{
    public ValidateOptionsResult Validate(string? name, OAuth2IntrospectionOptions options)
    {
        if (options.Authority.IsMissing() && options.IntrospectionEndpoint.IsMissing())
        {
            return ValidateOptionsResult.Fail("You must either set Authority or IntrospectionEndpoint");
        }
        return ValidateOptionsResult.Success;
    }
}
