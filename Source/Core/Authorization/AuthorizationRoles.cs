﻿using System;

namespace Exceptionless.Core.Authorization {
    public static class AuthorizationRoles {
        public const string Client = "client";
        public const string User = "user";
        public const string GlobalAdmin = "global";
        public static readonly string[] AllScopes = { "client", "user", "global" };
    }
}