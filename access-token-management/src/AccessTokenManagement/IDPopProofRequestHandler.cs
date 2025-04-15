// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

public interface IDPopProofRequestHandler
{
    Task<bool> TryAcquireDPopProof(HttpRequestMessage request, string? dpopNonce, ClientCredentialsToken token, CancellationToken cancellationToken);
    Task HandleDPopResponse(HttpResponseMessage response, CancellationToken cancellationToken);

}
