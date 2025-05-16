// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.AccessTokenManagement.OTel;
public static class ActivitySources
{

    public static ActivitySource Main = new(ActivitySourceNames.Main);
}

public static class ActivitySourceNames
{
    public static readonly string Main = typeof(ActivitySources).Assembly.GetName().Name!;
}

public static class ActivityNames
{
    public const string AcquiringToken = "Duende.AccessTokenManagement.AcquiringToken";
}
