using Duende.IdentityModel.OidcClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace Wpf
{
    internal class Session
    {
        private static readonly string SessionFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/session";

        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public IEnumerable<ClaimData> Claims { get; set; } = [];

        internal static void Store(LoginResult loginResult)
        {
            
            var session = new Session()
            {
                IdToken = loginResult.IdentityToken,
                RefreshToken = loginResult.RefreshToken,
                Claims = loginResult.User.Claims.Select(c => new ClaimData(c.Type, c.Value))
            };
            var plainText = JsonSerializer.Serialize(session);
            File.WriteAllText(SessionFile, DataProtector.Protect(plainText));
           
        }
        
        internal static Session? Get()
        {
            if (File.Exists(SessionFile))
            {
                var fileContent = File.ReadAllText(SessionFile);
                var unprotected = DataProtector.Unprotect(fileContent);
                return JsonSerializer.Deserialize<Session>(unprotected);
            }
            return null;
        }

        internal static void Delete()
        {
            File.Delete(SessionFile);
        }

    }
    internal record ClaimData(string Type, string Value);
}
