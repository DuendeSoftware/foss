# DPoP Extension Migration

This document describes the breaking changes made to DPoP extension methods as part of consolidating shared code into the IdentityModel library.

## Changes Made

### Moved to IdentityModel

The following extension methods have been moved from project-specific namespaces to `Duende.IdentityModel.DPoP`:

- `SetDPoPProofToken(this HttpRequestMessage request, string? proofToken)`
- `GetDPoPNonce(this HttpResponseMessage response)` - returns `string?`
- `GetDPoPUrl(this HttpRequestMessage request)` - returns `string`

### AccessTokenManagement Changes

To maintain compatibility, AccessTokenManagement now provides:

- `GetDPoPNonceValue(this HttpResponseMessage response)` - returns `DPoPNonce?` (parsed object)
- `GetDPoPUri(this HttpRequestMessage request)` - returns `Uri` (wrapper around shared method)
- `SetDPoPProofToken(this HttpRequestMessage request, DPoPProof proof)` - overload for DPoPProof type

### OidcClient Changes

The OidcClient project now uses the shared extensions from IdentityModel directly.

## Migration Guide

### For AccessTokenManagement users:

- Add `using Duende.IdentityModel.DPoP;` if you use the basic DPoP extension methods
- Replace `response.GetDPoPNonce()` with `response.GetDPoPNonceValue()` if you need the parsed `DPoPNonce?` object
- Replace `request.GetDPoPUrl()` with `request.GetDPoPUri()` if you need a `Uri` object

### For OidcClient users:

- Add `using Duende.IdentityModel.DPoP;` to access the shared DPoP extension methods
- The method signatures remain the same, only the namespace has changed

## Breaking Changes

This is a breaking change as the shared DPoP extension methods have moved to a new namespace: `Duende.IdentityModel.DPoP`.

Projects using these extensions will need to update their using statements to include the new namespace.