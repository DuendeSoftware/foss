// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;
using Duende.IdentityModel;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<Scheme>))]
public record Scheme : IStringValue<Scheme>
{
    public static implicit operator Scheme(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(50),
        ValidationRules.AlphaNumeric()
    ];

    public static readonly Scheme
        Bearer = Parse(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer);

    private Scheme(string value)
    {
        // since AccessTokenType above in the token endpoint response (the token_type value) could be case insensitive, but
        // when we send it as an Authorization header in the API request it must be case sensitive, we 
        // are checking for that here and forcing it to the exact casing required.
        if (value.Equals(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer, StringComparison.OrdinalIgnoreCase))
        {
            value = OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer;
        }
        else if (value.Equals(OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP, StringComparison.OrdinalIgnoreCase))
        {
            value = OidcConstants.AuthenticationSchemes.AuthorizationHeaderDPoP;
        }
        Value = value;
    }

    public string Value { get; }

    /// <summary>
    /// Used to represent an empty scheme for caching purposes
    /// </summary>
    internal static Scheme Empty = Load(string.Empty);

    public static bool TryParse(string value, [NotNullWhen(true)] out Scheme? parsed, out string[] errors) => IStringValue<Scheme>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    public static Scheme Load(string result) => new(result);

    public static Scheme Parse(string value) => StringParsers<Scheme>.Parse(value);

}
