// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.AccessTokenManagement.OpenIdConnect.Internal;
using Duende.AccessTokenManagement.OTel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.AccessTokenManagement;

public class UserAccessAccessTokenManagerTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAccessTokenAsync_with_null_identity_should_return_failure()
    {
        // A ClaimsPrincipal created with no arguments has Identity == null.
        // This happens when HttpContextUserAccessor falls back because
        // there is no HttpContext (e.g., background service).
        var user = new ClaimsPrincipal();

        var sut = CreateSut();

        var result = await sut.GetAccessTokenAsync(user, ct: _ct);

        result.Succeeded.ShouldBeFalse();
        result.FailedResult.ShouldNotBeNull();
        result.FailedResult.Error.ShouldBe("No active user");
    }

    [Fact]
    public async Task GetAccessTokenAsync_with_unauthenticated_identity_should_return_failure()
    {
        // A ClaimsPrincipal with an empty ClaimsIdentity is unauthenticated.
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var sut = CreateSut();

        var result = await sut.GetAccessTokenAsync(user, ct: _ct);

        result.Succeeded.ShouldBeFalse();
        result.FailedResult.ShouldNotBeNull();
        result.FailedResult.Error.ShouldBe("No active user");
    }

    private static UserAccessAccessTokenManager CreateSut()
    {
        var metrics = new AccessTokenManagementMetrics(new TestMeterFactory());
        var sync = new StubUserTokenRequestConcurrencyControl();
        var store = new StubUserTokenStore();
        var clock = TimeProvider.System;
        var options = Options.Create(new UserTokenManagementOptions());
        var tokenClient = new StubOpenIdConnectUserTokenEndpoint();
        var logger = new NullLogger<UserAccessAccessTokenManager>();

        return new UserAccessAccessTokenManager(
            metrics,
            sync,
            store,
            clock,
            options,
            tokenClient,
            logger);
    }

    private class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options) =>
            new(options);

        public void Dispose() { }
    }

    private class StubUserTokenRequestConcurrencyControl : IUserTokenRequestConcurrencyControl
    {
        public Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(
            UserRefreshToken key,
            Func<Task<TokenResult<UserToken>>> tokenRetriever,
            CancellationToken ct = default) =>
            tokenRetriever();
    }

    private class StubUserTokenStore : IUserTokenStore
    {
        public Task<TokenResult<TokenForParameters>> GetTokenAsync(
            ClaimsPrincipal user,
            UserTokenRequestParameters? parameters = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException("Should not be reached when user has no identity");

        public Task StoreTokenAsync(
            ClaimsPrincipal user,
            UserToken token,
            UserTokenRequestParameters? parameters = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException("Should not be reached when user has no identity");

        public Task ClearTokenAsync(
            ClaimsPrincipal user,
            UserTokenRequestParameters? parameters = null,
            CancellationToken ct = default) =>
            throw new NotImplementedException("Should not be reached when user has no identity");
    }

    private class StubOpenIdConnectUserTokenEndpoint : IOpenIdConnectUserTokenEndpoint
    {
        public Task<TokenResult<UserToken>> RefreshAccessTokenAsync(
            UserRefreshToken userToken,
            UserTokenRequestParameters parameters,
            CancellationToken ct = default) =>
            throw new NotImplementedException("Should not be reached when user has no identity");

        public Task RevokeRefreshTokenAsync(
            UserRefreshToken userToken,
            UserTokenRequestParameters parameters,
            CancellationToken ct = default) =>
            throw new NotImplementedException("Should not be reached when user has no identity");
    }
}
