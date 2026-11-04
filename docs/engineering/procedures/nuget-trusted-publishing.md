---
title: NuGet trusted publishing
description: Configure nuget.org trusted publishing for releases.
type: procedure
tags: process, products
---

# NuGet trusted publishing

All nuget.org releases use the `publish-nuget-org` job in `.github/workflows/release.yml`. After the existing `nuget.org` environment approval, the job uses `NuGet/login@v1` to exchange its GitHub OIDC token for a short-lived NuGet API key immediately before pushing signed packages. The job fails if authentication does not succeed or return a key; there is no long-lived API-key fallback.

GitHub Packages publishing and Azure Key Vault package signing are unchanged.

## Configure the publishing identity

Choose the nuget.org user profile that will authenticate publishing. Use its **profile name**, not its email address. The user must have publishing rights for every package released by this workflow, either directly or through the relevant nuget.org organization.

In the `DuendeSoftware/foss` repository's existing `nuget.org` GitHub environment, set the environment **variable** `NUGET_USER` to that profile name. The environment's existing approval rules continue to gate publication.

## Configure the nuget.org trust policy

Sign in to nuget.org as the publishing user, open **Trusted Publishing**, and create a GitHub policy with these settings:

| Setting | Value |
| --- | --- |
| Package owner | Select the nuget.org user for user-owned packages, or the organization that owns the packages for organization-owned packages |
| Repository owner | `DuendeSoftware` |
| Repository | `foss` |
| Workflow file | `release.yml` |
| Environment | `nuget.org` |

Enter only the workflow filename, not `.github/workflows/release.yml`. Grant the publishing scopes needed for new versions and, if releases introduce new package IDs, new packages. Restrict package patterns to the intended package families while ensuring they cover every package produced by all products in the release workflow.

The policy covers only packages owned by the selected nuget.org user or organization. Publishing rights do not extend a user's policy to organization-owned packages; if the workflow publishes packages owned by multiple organizations, configure a policy for each relevant organization.

## Verify trusted publishing

Configure the user variable and policy before dispatching a release. Use the normal release procedure and obtain the existing human environment approval; do not dispatch a release solely to test credentials.

In the publishing job, confirm the authentication step succeeds and all expected package versions appear on nuget.org. Workflow success alone is not sufficient if the authentication step was skipped or did not complete successfully. The action returns a temporary API key to the push step; the workflow does not print that key.

The `NUGET_ORG_API_KEY` secret is no longer used by this workflow. Confirm any remaining consumers before revoking or deleting that secret.

## References

- [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
- [NuGet login action](https://github.com/NuGet/login)
