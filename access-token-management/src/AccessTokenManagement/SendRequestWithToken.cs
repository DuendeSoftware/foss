// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.AccessTokenManagement;

/// <summary>
/// Delegate that dictates how a with access request is sent. 
/// </summary>
/// <param name="parameters">The parameters that controls how to send requests</param>
/// <param name="cancellationToken">cancellation token</param>
/// <returns></returns>
public delegate Task<HttpResponseMessage> SendRequestWithToken(AccessTokenHandlerRequestParameters parameters, CancellationToken cancellationToken);
