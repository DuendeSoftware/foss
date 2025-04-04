// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.IdentityModel.Internal;

internal static class HttpResponseMessageExtensions
{
    [DebuggerStepThrough]
    public static bool IsContentJwtMediaType(this HttpResponseMessage response)
    {
        var mediaType = response.Content?.Headers?.ContentType?.MediaType;

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType!.StartsWith("application/", StringComparison.OrdinalIgnoreCase) &&
               mediaType!.EndsWith("+jwt", StringComparison.OrdinalIgnoreCase);
    }
}
