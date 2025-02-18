﻿// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.Infrastructure;

namespace Duende.IdentityModel
{
    public abstract class DiscoveryPolicyTestsBase
    {
        private readonly IAuthorityValidationStrategy _authorityValidationStrategy;

        protected DiscoveryPolicyTestsBase(IAuthorityValidationStrategy authorityValidationStrategy)
        {
            _authorityValidationStrategy = authorityValidationStrategy;
        }

        protected DiscoveryPolicy ForceTestedAuthorityValidationStrategy(DiscoveryPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            policy.AuthorityValidationStrategy = _authorityValidationStrategy;
            return policy;
        }

        protected NetworkHandler GetHandler(string issuer, string endpointBase = null, string alternateEndpointBase = null)
        {
            if (endpointBase == null) endpointBase = issuer;
            if (alternateEndpointBase == null) alternateEndpointBase = issuer;

            var discoFileName = FileName.Create("discovery_variable.json");
            var raw = File.ReadAllText(discoFileName);

            var document = raw.Replace("{issuer}", issuer)
                              .Replace("{endpointBase}", endpointBase)
                              .Replace("{alternateEndpointBase}", alternateEndpointBase);

            var jwksFileName = FileName.Create("discovery_jwks.json");
            var jwks = File.ReadAllText(jwksFileName);

            var handler = new NetworkHandler(request =>
            {
                if (request.RequestUri.AbsoluteUri.EndsWith("jwks"))
                {
                    return jwks;
                }

                return document;
            }, HttpStatusCode.OK);

            return handler;
        }
    }
}
