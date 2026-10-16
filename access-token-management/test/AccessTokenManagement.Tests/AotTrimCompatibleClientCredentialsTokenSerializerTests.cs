// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Text.Json;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.Internal;

namespace Duende.AccessTokenManagement;

public class AotTrimCompatibleClientCredentialsTokenSerializerTests
{
    private TestData The { get; } = new();
    private TestDataBuilder Some => new(The);

    private static AotTrimCompatibleClientCredentialsTokenSerializer CreateSut() => new();

    [Fact]
    public void Deserialize_reads_cached_access_token_longer_than_default_limit()
    {
        var longAccessToken = new string('a', AccessToken.MaxLength + 1);
        var token = Some.ClientCredentialsToken() with { AccessToken = AccessToken.Parse(longAccessToken, AccessToken.MaxLength * 2) };
        var writer = new ArrayBufferWriter<byte>();
        CreateSut().Serialize(token, writer);

        var cached = CreateSut().Deserialize(new ReadOnlySequence<byte>(writer.WrittenMemory));

        cached.AccessToken.ToString().ShouldBe(longAccessToken);
    }

    [Fact]
    public void Serialized_cache_format_is_unchanged()
    {
        var token = Some.ClientCredentialsToken();
        var sut = CreateSut();

        var writer = new ArrayBufferWriter<byte>();
        sut.Serialize(token, writer);
        var previous = JsonSerializer.Deserialize(
            writer.WrittenSpan,
            DuendeAccessTokenSerializationContext.Default.ClientCredentialsToken);
        previous.ShouldBeEquivalentTo(token);

        var previousBytes = JsonSerializer.SerializeToUtf8Bytes(
            token,
            DuendeAccessTokenSerializationContext.Default.ClientCredentialsToken);
        var roundTripped = sut.Deserialize(new ReadOnlySequence<byte>(previousBytes));
        roundTripped.ShouldBeEquivalentTo(token);
        writer.WrittenSpan.ToArray().ShouldBe(previousBytes);
    }
}
