// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.AccessTokenManagement.OTel;

/// <summary>
/// Exposes activity sources used by access token management diagnostics.
/// </summary>
public static class ActivitySources
{

    /// <summary>
    /// The primary activity source for token management operations.
    /// </summary>
    public static ActivitySource Main = new(ActivitySourceNames.Main);
}

/// <summary>
/// Activity source names used by access token management diagnostics.
/// </summary>
public static class ActivitySourceNames
{
    /// <summary>
    /// The name used for <see cref="ActivitySources.Main"/>.
    /// </summary>
    public static readonly string Main = typeof(ActivitySources).Assembly.GetName().Name!;
}

/// <summary>
/// Activity names emitted by access token management instrumentation.
/// </summary>
public static class ActivityNames
{
    /// <summary>
    /// Activity name for token acquisition operations.
    /// </summary>
    public const string AcquiringToken = "Duende.AccessTokenManagement.AcquiringToken";
}
