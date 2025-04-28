// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.AccessTokenManagement.Implementation;

/// <summary>
/// Serialization context used by the DPoP proof service and the client credential token cache.
/// </summary>
[JsonSerializable(typeof(ClientCredentialsToken))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class DuendeAccessTokenSerializationContext : JsonSerializerContext
{
}
