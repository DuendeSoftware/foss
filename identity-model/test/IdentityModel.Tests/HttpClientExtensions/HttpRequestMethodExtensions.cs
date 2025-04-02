// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Duende.IdentityModel.HttpClientExtensions;

internal static class HttpRequestMethodExtensions
{
    public static IDictionary<string, object> GetProperties(this HttpRequestMessage requestMessage)
    {
#if NETFRAMEWORK
        return requestMessage.Properties;
#else
        return requestMessage.Options;
#endif
    }
}
