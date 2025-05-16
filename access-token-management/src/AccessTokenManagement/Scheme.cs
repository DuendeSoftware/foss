// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement.Internal;
using Duende.IdentityModel;

namespace Duende.AccessTokenManagement;

[TypeConverter(typeof(StringValueConverter<Scheme>))]
public readonly record struct Scheme : IStronglyTypedString<Scheme>
{
    public const int MaxLength = 50;
    public static implicit operator Scheme(string value) => Parse(value);

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        ValidationRules.MaxLength(MaxLength),
        ValidationRules.AlphaNumeric()
    ];

    public static readonly Scheme
        Bearer = Parse(OidcConstants.AuthenticationSchemes.AuthorizationHeaderBearer);
    public Scheme() => throw new InvalidOperationException("Can't create null value");

    private Scheme(string value)
    {

        // Some target systems are case-sensitive in their scheme handling. This code normalizes
        // the casing.

        // since AccessTokenType above in the token endpoint response (the token_type value) could be case-insensitive, but
        // when we send it as an Authorization header in the API request it must be case-sensitive, we
        // are checking for that here and forcing it to the exact casing required.

        //IE: if Scheme == BeAReR => "Bearer"
        //IE: if Scheme == DpoP => "DPoP"
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

    private string Value { get; }

    /// <summary>
    /// Used to represent an empty scheme for caching purposes
    /// </summary>
    internal static Scheme Empty = new(string.Empty);

    public static bool TryParse(string value, [NotNullWhen(true)] out Scheme? parsed, out string[] errors) =>
        IStronglyTypedString<Scheme>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static Scheme IStronglyTypedString<Scheme>.Create(string result) => new(result);

    public static Scheme Parse(string value) => StringParsers<Scheme>.Parse(value);

}
