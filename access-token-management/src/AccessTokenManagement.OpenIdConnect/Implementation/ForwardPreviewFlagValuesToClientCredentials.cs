// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// If you set the preview feature on UserTokenManagementOptions, this will forward it to the ClientCredentialsTokenManagementOptions.
/// </summary>
/// <param name="userTokenManagementOptions"></param>
internal class ForwardPreviewFlagValuesToClientCredentials(IOptions<UserTokenManagementOptions> userTokenManagementOptions) : IPostConfigureOptions<ClientCredentialsTokenManagementOptions>
{
    public void PostConfigure(string? name, ClientCredentialsTokenManagementOptions options) => options.UsePreviewExtensibilityOnAccessTokenHandlers = userTokenManagementOptions.Value.UsePreviewExtensibilityOnAccessTokenHandlers;
}
