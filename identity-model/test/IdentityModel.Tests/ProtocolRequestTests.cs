// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;

namespace Duende.IdentityModel;

public class ProtocolRequestTests
{
    [Fact]
    public void Clone_preserves_ClientAssertionFactory()
    {
        Func<Task<ClientAssertion>> factory = () => Task.FromResult(new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = "factory_jwt"
        });

        var original = new ProtocolRequest
        {
            ClientId = "client1",
            ClientAssertionFactory = factory
        };

        var clone = original.Clone();

        clone.ClientAssertionFactory.ShouldBeSameAs(factory);
    }

    [Fact]
    public void Clone_with_null_factory_leaves_factory_null()
    {
        var original = new ProtocolRequest
        {
            ClientId = "client1",
            ClientAssertionFactory = null
        };

        var clone = original.Clone();

        clone.ClientAssertionFactory.ShouldBeNull();
    }

    [Fact]
    public void Clone_copies_standard_properties()
    {
        var original = new ProtocolRequest
        {
            Address = "https://server/token",
            ClientId = "client1",
            ClientSecret = "secret",
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
        };
        original.Parameters.Add("custom", "value");

        var clone = original.Clone();

        clone.Address.ShouldBe(original.Address);
        clone.ClientId.ShouldBe(original.ClientId);
        clone.ClientSecret.ShouldBe(original.ClientSecret);
        clone.ClientCredentialStyle.ShouldBe(original.ClientCredentialStyle);
        clone.Parameters.ShouldContain(p => p.Key == "custom" && p.Value == "value");
    }
}
