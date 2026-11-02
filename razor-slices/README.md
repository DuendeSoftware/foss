# Duende.RazorSlices

This is a vendored fork of [DamianEdwards/RazorSlices](https://github.com/DamianEdwards/RazorSlices), maintained here for supply-chain control reasons. Vendoring allows Duende to pin the exact source, apply security patches independently, and ship the package under the `Duende.*` namespace without taking a transitive dependency on an externally-published NuGet package.

The upstream project is copyright Damian Edwards and licensed under the [Apache 2.0 license](../LICENSE). See [ThirdPartyNotices.txt](ThirdPartyNotices.txt) for the full attribution.

## Upstream sync point

Last synced to upstream commit [`c286ca0`](https://github.com/DamianEdwards/RazorSlices/commit/c286ca021b05345e74785364c4a14515b082c90b) (2026-09-19, `main` after DamianEdwards/RazorSlices#146). When re-syncing, copy upstream `src/`, `tests/`, and `samples/` source files, then apply the transformations listed below.

### Upstream changes applied on top of the sync point

- [`552937e`](https://github.com/DamianEdwards/RazorSlices/commit/552937e) (DamianEdwards/RazorSlices#149): fix encoding loops at buffer boundaries in `PipeWriterHtmlExtensions` and `Utf8PipeTextWriter`. The `TextWriterHtmlExtensions` part is superseded by the local fix in `85672bfd`, which replaced the manual encoding loop with `HtmlEncoder.Encode(TextWriter, ...)`.

## Differences from upstream

- Package ID, assembly name, and root namespace changed from `RazorSlices` to `Duende.RazorSlices`
- Source generator package renamed from `RazorSlices.SourceGenerator` to `Duende.RazorSlices.SourceGenerator`
- The source generator's model-type resolver matches the `Duende.RazorSlices` namespace when walking slice base types
- Samples keep the upstream `RazorSlices.Samples.*` namespace and add explicit `using Duende.RazorSlices;` directives where needed
- Duende license headers, LF line endings, and `dotnet format` applied
- Target framework updated to `net10.0`
- Strong-naming disabled (not required for vendored use)
