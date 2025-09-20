// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable 1591

namespace Duende.AspNetCore.Authentication.OAuth2Introspection.Infrastructure;

public class ClaimConverter : JsonConverter<Claim>
{
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(Utf8JsonReader, JsonSerializerOptions)")]
#pragma warning disable IL2046 // This method is properly annotated with RequiresUnreferencedCode, but base class does not have the same annotation.
    public override Claim Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
#pragma warning restore IL2046
    {
        var source = JsonSerializer.Deserialize<ClaimLite>(ref reader, options)!;
        var target = new Claim(source.Type, source.Value);

        return target;
    }

    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(Utf8JsonWriter, TValue, JsonSerializerOptions)")]
#pragma warning disable IL2046 // This method is properly annotated with RequiresUnreferencedCode, but base class does not have the same annotation.
    public override void Write(Utf8JsonWriter writer, Claim value, JsonSerializerOptions options)
#pragma warning restore IL2046
    {
        var target = new ClaimLite
        {
            Type = value.Type,
            Value = value.Value
        };

        JsonSerializer.Serialize(writer, target, options);
    }
}
