// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.AccessTokenManagement;
internal static class ActivitySources
{

    internal static ActivitySource Main = new ActivitySource(ActivitySourceNames.Main);
}

public static class ActivitySourceNames
{
    public const string Main = "Duende.AccessTokenManagement";
}

internal static class ActivityNames
{
    public const string AcquiringToken = "Duende.AccessTokenManagement.AcquiringToken";
}
