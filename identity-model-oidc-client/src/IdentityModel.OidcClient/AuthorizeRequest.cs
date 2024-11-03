﻿// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Duende.IdentityModel.OidcClient.Browser;
using IdentityModel.Client;

namespace Duende.IdentityModel.OidcClient
{
    class AuthorizeRequest
    {
        public DisplayMode DisplayMode { get; set; } = DisplayMode.Visible;
        public int Timeout { get; set; } = 300;
        
        public Parameters ExtraParameters = new Parameters();
    }
}