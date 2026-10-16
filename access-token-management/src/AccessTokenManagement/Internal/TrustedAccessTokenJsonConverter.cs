// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Reads <see cref="AccessToken"/> values from the cache without validation. ATM validated the token against the
/// configured limit before it wrote the entry.
/// </summary>
internal sealed class TrustedAccessTokenJsonConverter : JsonConverter<AccessToken>
{
    public override AccessToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value == null ? default : CreateUnvalidated<AccessToken>(value);
    }

    public override void Write(Utf8JsonWriter writer, AccessToken value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    private static T CreateUnvalidated<T>(string value) where T : struct, IStronglyTypedValue<T> => T.Create(value);
}
