// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace Duende.AccessTokenManagement.Internal;

/// <summary>
/// This class makes sure the ClientCredentialsToken is serialized in an AOT compatible way. 
/// </summary>
internal class AotTrimCompatibleClientCredentialsTokenSerializer : IHybridCacheSerializer<ClientCredentialsToken>
{
    public ClientCredentialsToken Deserialize(ReadOnlySequence<byte> source)
    {
        var reader = new Utf8JsonReader(source);

        return JsonSerializer.Deserialize(ref reader, DuendeAccessTokenSerializationContext.Default.ClientCredentialsToken)!;
    }

    public void Serialize(ClientCredentialsToken value, IBufferWriter<byte> target)
    {
        using var writer = new Utf8JsonWriter(target);

        JsonSerializer.Serialize<ClientCredentialsToken>(writer, value, DuendeAccessTokenSerializationContext.Default.ClientCredentialsToken);
    }
}
