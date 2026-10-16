// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

public class TokenMaxLengthOptionsValidationTests
{
    [Fact]
    public void Client_credentials_options_reject_non_positive_MaxAccessTokenLength()
    {
        var services = new ServiceCollection();
        services.AddClientCredentialsTokenManagement(o => o.MaxAccessTokenLength = 0);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ClientCredentialsTokenManagementOptions>>();

        var exception = Should.Throw<OptionsValidationException>(() => options.Value);
        exception.Message.ShouldContain(nameof(ClientCredentialsTokenManagementOptions.MaxAccessTokenLength));
    }

    [Fact]
    public void User_token_options_reject_non_positive_MaxAccessTokenLength() =>
        UserTokenOptionsValidationShouldFail(o => o.MaxAccessTokenLength = 0, "UserTokenManagementOptions.MaxAccessTokenLength");

    [Fact]
    public void User_token_options_reject_non_positive_MaxRefreshTokenLength() =>
        UserTokenOptionsValidationShouldFail(o => o.MaxRefreshTokenLength = 0, "UserTokenManagementOptions.MaxRefreshTokenLength");

    [Fact]
    public void User_token_options_reject_non_positive_MaxIdentityTokenLength() =>
        UserTokenOptionsValidationShouldFail(o => o.MaxIdentityTokenLength = 0, "UserTokenManagementOptions.MaxIdentityTokenLength");

    private static void UserTokenOptionsValidationShouldFail(Action<UserTokenManagementOptions> configure, string expectedOptionName)
    {
        var services = new ServiceCollection();
        services.AddOpenIdConnectAccessTokenManagement(configure);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<UserTokenManagementOptions>>();

        var exception = Should.Throw<OptionsValidationException>(() => options.Value);
        exception.Message.ShouldContain(expectedOptionName);
    }
}
