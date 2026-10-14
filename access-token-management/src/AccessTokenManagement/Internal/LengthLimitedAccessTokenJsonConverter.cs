// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// Reads <see cref="AccessToken"/> values using a configured maximum length. The written format is the same plain
/// string as the type-level converter.
/// </summary>
internal sealed class LengthLimitedAccessTokenJsonConverter(int maxLength, string optionName) : JsonConverter<AccessToken>
{
    public override AccessToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value == null
            ? default
            : TokenParsing.ParseAccessToken(value, maxLength, optionName);
    }

    public override void Write(Utf8JsonWriter writer, AccessToken value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
