// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace WebClientAssertions;

public abstract class TypedClient(HttpClient client)
{
    public virtual async Task<string> CallApi() => await client.GetStringAsync("test");
}

public class TypedUserClient(HttpClient client) : TypedClient(client);

public class TypedClientClient(HttpClient client) : TypedClient(client);
