// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Duende.AccessTokenManagement.Internal;
using Microsoft.IdentityModel.Tokens;

namespace Duende.AccessTokenManagement.Types;

[TypeConverter(typeof(StringValueConverter<DPoPJsonWebKey>))]
[JsonConverter(typeof(StringValueJsonConverter<DPoPJsonWebKey>))]
public record DPoPJsonWebKey : IStringValue<DPoPJsonWebKey>
{
    public virtual bool Equals(DPoPJsonWebKey? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value == other.Value;
    }

    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator DPoPJsonWebKey(string value) => Parse(value);

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
                message = "String is not a valid json webkey: " + e.Message;
                return false;
            }
        };

    private DPoPJsonWebKey(string value)
    {
        Value = value;
        JsonWebKey = new JsonWebKey(value);
    }

    public string Value { get; }

    public JsonWebKey JsonWebKey { get; }


    public static bool TryParse(string value, [NotNullWhen(true)] out DPoPJsonWebKey? parsed, out string[] errors) =>
        IStringValue<DPoPJsonWebKey>.TryBuildValidatedObject(value, Validators, out parsed, out errors);


    public static DPoPJsonWebKey Load(string result) => new(result);

    public static DPoPJsonWebKey Parse(string value) => StringParsers<DPoPJsonWebKey>.Parse(value);
    public static DPoPJsonWebKey? ParseOrDefault(string? value) => StringParsers<DPoPJsonWebKey>.ParseOrDefault(value);

}
