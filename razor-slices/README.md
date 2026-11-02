# Duende.RazorSlices

This is a vendored fork of [DamianEdwards/RazorSlices](https://github.com/DamianEdwards/RazorSlices), maintained here for supply-chain control reasons. Vendoring allows Duende to pin the exact source, apply security patches independently, and ship the package under the `Duende.*` namespace without taking a transitive dependency on an externally-published NuGet package.

The upstream project is copyright Damian Edwards and licensed under the [Apache 2.0 license](../LICENSE). See [ThirdPartyNotices.txt](ThirdPartyNotices.txt) for the full attribution.

## Upstream sync point

Last synced to upstream commit [`c286ca0`](https://github.com/DamianEdwards/RazorSlices/commit/c286ca021b05345e74785364c4a14515b082c90b) (2026-09-19, `main` after DamianEdwards/RazorSlices#146). When re-syncing, copy upstream `src/`, `tests/`, and `samples/` source files, then apply the transformations listed below.

### Upstream changes applied on top of the sync point

- [`552937e`](https://github.com/DamianEdwards/RazorSlices/commit/552937e) (DamianEdwards/RazorSlices#149): fix encoding loops at buffer boundaries in `PipeWriterHtmlExtensions` and `Utf8PipeTextWriter`. The `TextWriterHtmlExtensions` part is superseded by the local fix in `85672bfd`, which replaced the manual encoding loop with `HtmlEncoder.Encode(TextWriter, ...)`.
- [`190e80a`](https://github.com/DamianEdwards/RazorSlices/commit/190e80a) (DamianEdwards/RazorSlices#150): emit qualified names for primitive model types in generated proxies.
- [`0bdabc2`](https://github.com/DamianEdwards/RazorSlices/commit/0bdabc2) (DamianEdwards/RazorSlices#154): shareable Razor helpers (`RazorSliceHelpers`), minimal slices built on the new `RazorSliceBase` (the `RazorSlice` partials split into `RazorSliceBase.*`), generated slice/helper activation, and the helper analyzer. Samples and tests are ported too. The `BuildTasks` changes and the `BuildTasks`/`Samples.WebApp` test projects are not vendored, so they were left out.
- [`44409f8`](https://github.com/DamianEdwards/RazorSlices/commit/44409f8) (DamianEdwards/RazorSlices#158), diagnostic-ID rename only: source generator and analyzer diagnostics renamed from `RSG####` to `RSLICE####`. The rest of that PR was not taken: the experimental TempData provider, the form/antiforgery helpers, the `RSLICE0200` experimental diagnostic, and their samples and tests.
- [`a4eadb5`](https://github.com/DamianEdwards/RazorSlices/commit/a4eadb5) (DamianEdwards/RazorSlices#161), bug fixes only:
  - templated Razor delegates (`HelperResult`) are rejected only when their `Task` is incomplete;
  - a rejected writer is never returned to the pool;
  - faults are observed;
  - the source generator project reference is isolated from consumer publish properties (`PublishAot`, `RuntimeIdentifier`, ...);
  - the dead `NET10_0_OR_GREATER` conditionals in `IResultExtensions` are dropped.

  The .NET 11 / multi-targeting, CI, benchmark and sample changes were not taken.

Other upstream changes after the sync point have not been taken. These are:
- DamianEdwards/RazorSlices#147 (branding);
- DamianEdwards/RazorSlices#151 (upstream README only);
- DamianEdwards/RazorSlices#152 (version bump);
- DamianEdwards/RazorSlices#156 (build-task MVC property optimizer, not vendored);
- DamianEdwards/RazorSlices#159 (streaming Tag Helpers spike);
- DamianEdwards/RazorSlices#35 (multi-TFM publish example);
- DamianEdwards/RazorSlices#163 (HTMX todo sample).

## Differences from upstream

- Package ID, assembly name, and root namespace changed from `RazorSlices` to `Duende.RazorSlices`
- Source generator package renamed from `RazorSlices.SourceGenerator` to `Duende.RazorSlices.SourceGenerator`
- The source generator's model-type resolver matches the `Duende.RazorSlices` namespace when walking slice base types
- Samples keep the upstream `RazorSlices.Samples.*` namespace and add explicit `using Duende.RazorSlices;` directives where needed
- Duende license headers, LF line endings, and `dotnet format` applied
- Target framework updated to `net10.0`; source that needs preview C# features (for example collection expression arguments, `[with(...), ...]`) is rewritten to C# 14 equivalents
- Strong-naming disabled (not required for vendored use)
