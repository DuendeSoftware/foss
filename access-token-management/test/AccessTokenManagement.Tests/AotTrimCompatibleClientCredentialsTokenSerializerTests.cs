// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Buffers;
using System.Text.Json;
using Duende.AccessTokenManagement.Framework;
using Duende.AccessTokenManagement.Internal;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

public class AotTrimCompatibleClientCredentialsTokenSerializerTests
{
    private TestData The { get; } = new();
    private TestDataBuilder Some => new(The);

    private static AotTrimCompatibleClientCredentialsTokenSerializer CreateSut(int maxAccessTokenLength) =>
        new(Options.Create(new ClientCredentialsTokenManagementOptions { MaxAccessTokenLength = maxAccessTokenLength }));

    [Fact]
    public void Deserialize_rejects_cached_access_token_longer_than_configured_limit()
    {
        var token = Some.ClientCredentialsToken() with { AccessToken = AccessToken.Parse(new string('a', 200), 1000) };
        var writer = new ArrayBufferWriter<byte>();
        CreateSut(1000).Serialize(token, writer);

        var exception = Should.Throw<InvalidOperationException>(() =>
            CreateSut(100).Deserialize(new ReadOnlySequence<byte>(writer.WrittenMemory)));

        exception.Message.ShouldContain(
            $"{nameof(ClientCredentialsTokenManagementOptions)}.{nameof(ClientCredentialsTokenManagementOptions.MaxAccessTokenLength)}");
    }

    [Fact]
    public void Serialized_cache_format_is_unchanged()
    {
        var token = Some.ClientCredentialsToken();
        var sut = CreateSut(AccessToken.MaxLength);

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
