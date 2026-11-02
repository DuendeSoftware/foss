# Duende Free and Open Source Software
This repository contains Duende's Free and Open Source Software.

- [Duende.AccessTokenManagement](/access-token-management/) .NET libraries that
  manage OAuth and OpenId Connect access tokens.
- [Duende.AspNetCore.Authentication.OAuth2Introspection](/introspection) ASP.NET Authentication
  handler for OAuth 2.0 introspection.
- [Duende.IdentityModel](/identity-model/) .NET libraries for claims-based
  identity, OAuth 2.0 and OpenID Connect.
- [Duende.IdentityModel.OidcClient](/identity-model-oidc-client/) .NET libraries
  for building OpenID Connect (OIDC) native clients

## Relationship to IdentityModel
Several of the libraries within this repository used to be found in the
[IdentityModel](https://github.com/IdentityModel/IdentityModel) GitHub organization. Those
projects have been renamed with the prefix "Duende" to reflect that they are all
maintained by Duende Software and are designed to work well together. The name change
doesn't indicate other big changes: Duende Software is the company Brock and Dominick
founded, they remain involved in these libraries, and we are committed to keeping them
open source.

## License
Duende's open source  projects are released under the [Apache 2.0 license](/LICENSE).

## Contributions
- For bug reports, please create an [issue](https://github.com/DuendeSoftware/foss/issues).
- For support, feature requests, proposals, etc, please start a [discussion](https://github.com/orgs/DuendeSoftware/discussions).

## Code scanning
The CodeQL workflow scans pull requests targeting `main`, pushes to `main`, and
the default branch weekly. It uses the SDK in `global.json` and manual Release
builds of every project beneath each top-level product's `src` directory. This
includes all seven products, the RazorSlices source generator, and trimming
analysis projects; samples, clients, tests, and performance projects are not
part of this scan. Building the source projects explicitly avoids selecting
Windows-only samples on the Ubuntu runner and preserves build-time generation
and dependency information that no-build extraction can miss.

When adding production projects, keep them under a product's `src` directory
so they are included automatically. To reproduce the build locally, use the
`Build production source` PowerShell block in `.github/workflows/codeql.yml`.
A successful local build does not verify CodeQL extraction or upload.

After changing the workflow, verify the extracted project/file coverage in
CodeQL's logs and tool-status diagnostics, including each product and the
RazorSlices source generator. Confirm that analysis and SARIF processing
completed without extraction or analysis errors for the intended commit and
`/language:csharp` category. After the change reaches `main`, verify a new
main-branch analysis at that commit (or a known descendant) and re-fetch the
alerts. A green workflow alone, or old alert instances, is not evidence that
the current production source was analyzed. Scorecard findings must likewise
be reassessed from a fresh Scorecard run, not dismissed based on this change.
