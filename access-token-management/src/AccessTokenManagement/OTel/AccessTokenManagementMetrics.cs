// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Diagnostics.Metrics;

namespace Duende.AccessTokenManagement.OTel;


public sealed class AccessTokenManagementMetrics
{
    public const string MeterName = "Duende.AccessTokenManagement";

    private readonly Counter<int> _accessTokenUsed;
    private readonly Counter<int> _tokenRetrieved;
    private readonly Counter<int> _tokenRetrievalFailed;
    private readonly Counter<int> _accessTokenAccessDeniedRetry;
    private readonly Counter<int> _dpopNonceErrorRetry;

    public AccessTokenManagementMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _accessTokenUsed = meter.CreateCounter<int>("access_token_used",
            unit: "Count",
            description: "The number of times an access token was used.");

        _tokenRetrieved = meter.CreateCounter<int>("token_retrieved",
            unit: "Count",
            description: "The number of times an access token was retrieved from the Token Provider. ");

        _tokenRetrievalFailed = meter.CreateCounter<int>("token_retrieval_failed",
            unit: "Count",
            description: "Then number of times retrieval of tokens from the Token Provider failed.");

        _accessTokenAccessDeniedRetry = meter.CreateCounter<int>("token_send_retry",
            unit: "Count",
            description: "The number of times an access token was not accepted but will be retried.");

        _dpopNonceErrorRetry = meter.CreateCounter<int>("dpop_nonce_error_retry",
            unit: "Count",
            description: "The number of times the target system replied with a DPoP Nonce error which will be retried.");
    }


    /// <summary>
    /// The type of token request to be stored in the metric
    /// </summary>
    public enum TokenRequestType
    {
        ClientCredentials = 1,
        User = 2
    }

    /// <summary>
    /// Writes a metric when an access token is actually used
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="type"></param>
    public void AccessTokenUsed(ClientId? clientId, TokenRequestType type)
    {
        if (!_accessTokenUsed.Enabled)
        {
            return;
        }

        _accessTokenUsed.Add(1,
            new KeyValuePair<string, object?>(OTelParameters.ClientId, clientId),
            new KeyValuePair<string, object?>(OTelParameters.TokenType, type.ToString())
            );
    }

    /// <summary>
    /// Writes a metric when an access token is retrieved
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="type"></param>
    public void TokenRetrieved(string? clientId, TokenRequestType type)
    {
        if (!_tokenRetrieved.Enabled)
        {
            return;
        }

        _tokenRetrieved.Add(1,
            new KeyValuePair<string, object?>(OTelParameters.ClientId, clientId),
            new KeyValuePair<string, object?>(OTelParameters.TokenType, type.ToString())
            );
    }

    /// <summary>
    /// Writes a metric when an access token retrieval fails
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="type"></param>
    /// <param name="error"></param>
    public void TokenRetrievalFailed(string? clientId, TokenRequestType type, string? error)
    {
        if (!_tokenRetrievalFailed.Enabled)
        {
            return;
        }
        _tokenRetrievalFailed.Add(1,
            new KeyValuePair<string, object?>(OTelParameters.ClientId, clientId),
            new KeyValuePair<string, object?>(OTelParameters.Error, error),
            new KeyValuePair<string, object?>(OTelParameters.TokenType, type.ToString())
        );
    }

    /// <summary>
    /// Writes a metric when an access token is retried
    /// </summary>
    /// <param name="clientId"></param>
    public void AccessTokenAccessDeniedRetry(ClientId? clientId)
    {
        if (!_accessTokenAccessDeniedRetry.Enabled)
        {
            return;
        }
        _accessTokenAccessDeniedRetry.Add(1,
            new KeyValuePair<string, object?>(OTelParameters.ClientId, clientId?.ToString())
        );
    }

    public void DPoPNonceErrorRetry(ClientId? clientId, string? error)
    {
        if (!_dpopNonceErrorRetry.Enabled)
        {
            return;
        }
        _dpopNonceErrorRetry.Add(1,
            new KeyValuePair<string, object?>(OTelParameters.ClientId, clientId?.ToString()),
            new KeyValuePair<string, object?>(OTelParameters.Error, error)
        );
    }
}


