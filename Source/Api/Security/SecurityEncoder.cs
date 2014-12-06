using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Security;

namespace Exceptionless.Api.Security {
    public class SecurityEncoder {
        private const string MACHINE_KEY_PURPOSE = "Membership:Username:{0}";
        private const string ANONYMOUS = "<anonymous>";
        private const int SALT_SIZE = 128 / 8; // 128 bits

        public string GetSaltedHash(string plainText, string salt) {
            return EncodePassword(plainText, salt);
        }

        public string Protect(string data) {
            using (var ms = new MemoryStream()) {
                using (var bw = new BinaryWriter(ms)) {
                    bw.Write(data);
                    bw.Flush();
                    var serializedWithPadding = new byte[ms.Length + _padding.Length];
                    Buffer.BlockCopy(_padding, 0, serializedWithPadding, 0, _padding.Length);
                    Buffer.BlockCopy(ms.GetBuffer(), 0, serializedWithPadding, _padding.Length, (int)ms.Length);

                    return Protect(serializedWithPadding);
                }
            }
        }

        public bool TryUnprotect(string protectedData, out string data) {
            data = null;
            if (String.IsNullOrEmpty(protectedData))
                return false;

            byte[] decodedWithPadding = Unprotect(protectedData);
            if (decodedWithPadding.Length < _padding.Length)
                return false;

            // timing attacks aren't really applicable to this, so we just do the simple check.
            if (_padding.Where((b, index) => b != decodedWithPadding[index]).Any())
                return false;

            using (var ms = new MemoryStream(decodedWithPadding, _padding.Length, decodedWithPadding.Length - _padding.Length)) {
                using (var br = new BinaryReader(ms)) {
                    try {
                        // use temp variable to keep both out parameters consistent and only set them when the input stream is read completely
                        string tempData = br.ReadString();
                        // make sure that we consume the entire input stream
                        if (ms.ReadByte() == -1) {
                            data = tempData;
                            return true;
                        }
                    } catch {
                        // Any exceptions will result in this method returning false.
                    }
                }
            }

            return false;
        }

        public string GenerateSalt() {
            var buf = new byte[SALT_SIZE];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(buf);

            return Convert.ToBase64String(buf);
        }

        private string EncodePassword(string password, string salt) {
            byte[] passwordBytes = Encoding.Unicode.GetBytes(password);
            byte[] saltBytes = Convert.FromBase64String(salt);

            var hashStrategy = HashAlgorithm.Create("HMACSHA256") as KeyedHashAlgorithm;
            if (hashStrategy.Key.Length == saltBytes.Length)
                hashStrategy.Key = saltBytes;
            else if (hashStrategy.Key.Length < saltBytes.Length) {
                var keyBytes = new byte[hashStrategy.Key.Length];
                Buffer.BlockCopy(saltBytes, 0, keyBytes, 0, keyBytes.Length);
                hashStrategy.Key = keyBytes;
            } else {
                var keyBytes = new byte[hashStrategy.Key.Length];
                for (int i = 0; i < keyBytes.Length;) {
                    int len = Math.Min(saltBytes.Length, keyBytes.Length - i);
                    Buffer.BlockCopy(saltBytes, 0, keyBytes, i, len);
                    i += len;
                }
                hashStrategy.Key = keyBytes;
            }
            byte[] result = hashStrategy.ComputeHash(passwordBytes);
            return Convert.ToBase64String(result);
        }

        private static readonly byte[] _padding = new byte[] { 0x85, 0xC5, 0x65, 0x72 };

        // .NET 4.5 specific implementation. http://brockallen.com/2012/06/21/use-the-machinekey-api-to-protect-values-in-asp-net/
        private string Protect(byte[] data) {
            if (data == null || data.Length == 0)
                return null;

            string purpose = GetMachineKeyPurpose(Thread.CurrentPrincipal);
            byte[] value = MachineKey.Protect(data, purpose);

            return Convert.ToBase64String(value);
        }

        private byte[] Unprotect(string value) {
            if (String.IsNullOrWhiteSpace(value))
                return null;
            string purpose = GetMachineKeyPurpose(Thread.CurrentPrincipal);
            byte[] bytes = Convert.FromBase64String(value);

            return MachineKey.Unprotect(bytes, purpose);
        }

        private string GetMachineKeyPurpose(IPrincipal user) {
            return String.Format(MACHINE_KEY_PURPOSE, user.Identity.IsAuthenticated ? user.Identity.Name : ANONYMOUS);
        }
    }
}