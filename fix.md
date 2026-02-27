## Summary

When a token endpoint returns a DPoP nonce (401 with `DPoP-Nonce` header), `ProofTokenMessageHandler` retries the request using the **same request object**, which carries the **same `ClientAssertion`** (same `jti`, `iat`, etc.). Servers that enforce client assertion uniqueness (e.g. HelseID) reject the retry as a replay.

Reported in: https://github.com/DuendeSoftware/foss/issues/323

## Root Cause

The retry path in `ProofTokenMessageHandler.SendAsync()` reuses the original `HttpRequestMessage`. The `ClientAssertion` is set once at request creation time (e.g. in `ResponseProcessor.RedeemCodeAsync()`) and never regenerated for the retry.

Additionally, `ProtocolRequest.Clone<T>()` copies `ClientAssertion` **by reference** (shallow copy), so even cloned requests share the same assertion object — though the real issue is the JWT string itself being identical.

### Call chain

```
ResponseProcessor.RedeemCodeAsync()
  → AuthorizationCodeTokenRequest created with ClientAssertion (fetched once)
    → HttpClientTokenRequestExtensions clones via ProtocolRequest.Clone<T>()
      → Clone() shallow-copies ClientAssertion
        → ProofTokenMessageHandler.SendAsync() retries on DPoP nonce
          → Same ClientAssertion reused on retry ← BUG
```

## Affected Code

| File | Package | Role |
|------|---------|------|
| `ProofTokenMessageHandler.cs` (L42-44) | oidc-client-extensions | Retries request with same object on DPoP nonce |
| `ProtocolRequest.cs` (L103) | identity-model | `Clone()` shallow-copies `ClientAssertion` |
| `ResponseProcessor.cs` (L184) | oidc-client | Sets client assertion once at request creation |
| `HttpClientTokenRequestExtensions.cs` | identity-model | Calls `Clone()` + `Prepare()` on all token requests |
| `AuthorizationServerDPoPHandler.cs` | access-token-management | Same retry pattern, same bug |

## Scope

Affects **all token request types** using client assertions + DPoP:
- Authorization code exchange
- Refresh token requests
- Client credentials requests
- PAR requests
- Device authorization

## Expected Behavior

Each token request attempt (including DPoP nonce retries) should use a **unique client assertion** with a fresh `jti` and `iat`, per RFC 7521 §4.2.

---

## Proposed Fix: Factory on `HttpRequestMessage.Options` (Strategy C)

### Approach

Add a `ClientAssertionFactory` (`Func<Task<ClientAssertion>>?`) property to `ProtocolRequest` that flows through `Clone()` → `Prepare()` → `HttpRequestMessage.Options` → handler chain. On DPoP nonce retry, `ProofTokenMessageHandler` checks for the factory, calls it to get a fresh assertion, and rebuilds the form body.

### Why this strategy

- **Backward compatible** — additive only, defaults to null, no behavioral change when unset
- **Minimal coupling** — the handler only needs to know about `client_assertion` / `client_assertion_type` form fields
- **Uses standard .NET patterns** — `HttpRequestMessage.Options` is the idiomatic way to pass data through handler chains
- **Works at the right level** — the factory is set by callers who have access to key material, and consumed by the handler that performs the retry

### Alternatives considered and rejected

| Strategy | Why rejected |
|----------|-------------|
| A: Pass factory directly to handler constructor | Handler is a `DelegatingHandler` — can't easily access per-request assertion factories |
| B: Call `Prepare()` again on retry | `Prepare()` is not idempotent (double-adds parameters) and is synchronous |
| D: Move retry to callers | Major architectural change, duplicates retry logic across all callers |

### Implementation tasks

#### identity-model

- [ ] Add `Func<Task<ClientAssertion>>? ClientAssertionFactory` property to `ProtocolRequest`
- [ ] Copy `ClientAssertionFactory` in `Clone<T>()`
- [ ] Define a public `HttpRequestOptionsKey` for the factory (e.g. `ProtocolRequestOptions.ClientAssertionFactory`)
- [ ] Store the factory on `HttpRequestMessage.Options` in `RequestTokenAsync()` after `Prepare()`
- [ ] Also store in `PushAuthorizationAsync()` for PAR flow
- [ ] Update public API verification snapshot

#### identity-model-oidc-client

- [ ] Update `ProofTokenMessageHandler.SendAsync()` retry path to:
  1. Check for factory in `request.Options`
  2. Call factory to get fresh `ClientAssertion`
  3. Parse existing form body as `IEnumerable<KeyValuePair<string, string>>` (preserving duplicate keys)
  4. Replace `client_assertion` and `client_assertion_type` values
  5. Rebuild `FormUrlEncodedContent`
- [ ] Wire `ClientAssertionFactory` in `ResponseProcessor.RedeemCodeAsync()`
- [ ] Wire `ClientAssertionFactory` in `OidcClient.RefreshTokenAsync()`
- [ ] Wire `ClientAssertionFactory` in `AuthorizeClient.PushAuthorizationRequestAsync()`

#### access-token-management

- [ ] Fix `AuthorizationServerDPoPHandler` retry path (same pattern as `ProofTokenMessageHandler`)
- [ ] Evaluate `ClientCredentialsTokenClient` and `OpenIdConnectUserTokenEndpoint` retry paths — they reuse the same `request.ClientAssertion` value across retries

#### Tests

- [ ] Unit test: `ProofTokenMessageHandler` uses fresh assertion on DPoP nonce retry
- [ ] Unit test: `Clone<T>()` preserves `ClientAssertionFactory`
- [ ] Unit test: backward compatibility — no factory set, behavior unchanged
- [ ] Integration test: end-to-end DPoP + client assertion with nonce enforcement

### Implementation notes

- Form body parsing on retry **must** use `IEnumerable<KeyValuePair<string, string>>`, not a dictionary — dictionaries collapse duplicate keys like `resource` (identified during security review)
- Both `client_assertion` and `client_assertion_type` should be replaced from the factory result to guard against type mismatches
- `FormUrlEncodedContent` buffers internally, so `ReadAsStringAsync()` works after the first send
