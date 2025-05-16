// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.DPoP;

[TypeConverter(typeof(StringValueConverter<ProofKeyString>))]
[JsonConverter(typeof(StringValueJsonConverter<ProofKeyString>))]
public readonly record struct ProofKeyString : IStronglyTypedString<ProofKeyString>
{
    public bool Equals(ProofKeyString other) => Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator ProofKeyString(string value) => Parse(value);
    public static implicit operator string(ProofKeyString value) => value.ToString();

    public override string ToString() => Value;

    private static readonly ValidationRule<string>[] Validators = [
        // Officially, there's no max length for JWTs, but 32k is a good limit
        ValidationRules.MaxLength(32 * 1024),
        IsValidJsonWebKey()
    ];

    private static ValidationRule<string> IsValidJsonWebKey() =>
        (string value, out string message) =>
        {
            message = string.Empty;
            try
            {
                JsonWebKey.Create(value);
                return true;
            }
            catch (Exception e)
            {
                message = "String is not a valid json web key: " + e.Message;
                return false;
            }
        };

    public ProofKeyString() => throw new InvalidOperationException("Can't create null value");
    private ProofKeyString(string value) => Value = value;

    private string Value { get; }

    public static bool TryParse(string value, [NotNullWhen(true)] out ProofKeyString? parsed, out string[] errors) =>
        IStronglyTypedString<ProofKeyString>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    static ProofKeyString IStronglyTypedString<ProofKeyString>.Create(string result) => new(result);

    public static ProofKeyString Parse(string value) => StringParsers<ProofKeyString>.Parse(value);
    public static ProofKeyString? ParseOrDefault(string? value) => StringParsers<ProofKeyString>.ParseOrDefault(value);

}
