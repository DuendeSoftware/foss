using System;
using System.Security.Cryptography;
using System.Text;

namespace Wpf
{
    internal static class DataProtector
    {
        internal static string Protect(string plainText)
        {
            byte[] encryptedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        internal static string Unprotect(string encryptedText)
        {
            var encryptedData = Convert.FromBase64String(encryptedText);
            var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedData);
        }
    }
}
