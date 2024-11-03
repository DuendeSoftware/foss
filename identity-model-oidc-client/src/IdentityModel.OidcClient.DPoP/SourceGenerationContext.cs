// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityModel.OidcClient.DPoP
{
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Metadata,
	    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(JsonWebKey))]
    [JsonSerializable(typeof(DPoPProofPayload))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}