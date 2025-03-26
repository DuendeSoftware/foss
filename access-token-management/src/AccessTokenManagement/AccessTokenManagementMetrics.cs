// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Diagnostics.Metrics;

namespace Duende.AccessTokenManagement;
public class Metrics
{
    private const string MeterName = "Duende.AccessTokenManagement";

    private readonly Meter Meter;
    private readonly Counter<int> AccessTokenUsedCounter;
    private readonly Counter<int> TokenRetrievedCounter;
    private readonly Counter<int> TokenRetrievalFailedCounter;
    private readonly Counter<int> AccessTokenExpiredCounterCounter;
    private readonly Counter<int> AuthenticationFailedCounterCounter;

    public Metrics(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(MeterName);

        AccessTokenUsedCounter = Meter.CreateCounter<int>("access_token_used");
        TokenRetrievedCounter = Meter.CreateCounter<int>("token_retrieved");
        TokenRetrievalFailedCounter = Meter.CreateCounter<int>("token_retrieval_failed");
        AccessTokenExpiredCounterCounter = Meter.CreateCounter<int>("access_token_expired");
        AuthenticationFailedCounterCounter = Meter.CreateCounter<int>("authentication_failed");
    }


    private class Parameters
    {
        public const string ClientId = "client-id";
        public const string AccessTokenType = "type";
        public const string Error = "error";
    }

    /// <summary>
    /// Writes a metric when an access token is actually used
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="type"></param>
    public void AccessTokenUsed(string? clientId, TokenRequestType type)
    {
        if (!AccessTokenUsedCounter.Enabled)
        {
            return;
        }

        AccessTokenUsedCounter.Add(1, 
            new KeyValuePair<string, object?>(Parameters.ClientId, clientId),
            new KeyValuePair<string, object?>(Parameters.AccessTokenType, type.ToString())
            );
    }

    public void TokenRetrieved(string? clientId, TokenRequestType type)
    {
        if (!TokenRetrievedCounter.Enabled)
        {
            return;
        }

        TokenRetrievedCounter.Add(1, 
            new KeyValuePair<string, object?>(Parameters.ClientId, clientId),
            new KeyValuePair<string, object?>(Parameters.AccessTokenType, type.ToString())
            );
    }

    public void TokenRetrievalFailed(string? clientId, TokenRequestType type, string? error)
    {
        if (!TokenRetrievalFailedCounter.Enabled)
        {
            return;
        }
        TokenRetrievalFailedCounter.Add(1,
            new KeyValuePair<string, object?>(Parameters.ClientId, clientId),
            new KeyValuePair<string, object?>(Parameters.Error, error),
            new KeyValuePair<string, object?>(Parameters.AccessTokenType, type.ToString())
        );
    }

    public enum TokenRequestType
    {
        ClientCredentials = 1,
        User = 2,
        UserDPoP = 3,
    }

}


