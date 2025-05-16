// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Duende.IdentityModel.Client;

namespace Duende.AccessTokenManagement.Tests;

public class TestClientAssertionService(string name, string assertionType, string assertionValue)
    : IClientAssertionService
{
    public Task<ClientAssertion?> GetClientAssertionAsync(ClientName? clientName = null, TokenRequestParameters? parameters = null, CancellationToken ct = default)
    {
        if (clientName == name)
        {
            return Task.FromResult<ClientAssertion?>(new()
            {
                Type = assertionType,
                Value = assertionValue
            });
        }

        return Task.FromResult<ClientAssertion?>(null);
    }
}
