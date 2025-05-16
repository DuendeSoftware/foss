// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.AccessTokenManagement.Internal;

internal class StringValueJsonConverter<TSelf> : JsonConverter<TSelf> where TSelf : struct, IStronglyTypedString<TSelf>
{
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value == null
            ? default
            : TSelf.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}
