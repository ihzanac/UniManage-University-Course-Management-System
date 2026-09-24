using System;
using System.Security.Cryptography;
using Microsoft.AspNet.Identity;

namespace UniManage
{
    /// <summary>
    /// Supports both ASP.NET Identity 2 hashes and ASP.NET Core Identity hashes.
    /// This lets migrated users keep their existing passwords.
    /// </summary>
    public sealed class HybridPasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher _identity2Hasher = new PasswordHasher();

        public string HashPassword(string password)
        {
            return _identity2Hasher.HashPassword(password);
        }

        public PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword)
        {
            var identity2Result = _identity2Hasher.VerifyHashedPassword(hashedPassword, providedPassword);
            if (identity2Result != PasswordVerificationResult.Failed)
            {
                return identity2Result;
            }

            if (VerifyAspNetCoreIdentityHash(hashedPassword, providedPassword))
            {
                // Rehash to Identity2 format on next successful sign-in.
                return PasswordVerificationResult.SuccessRehashNeeded;
            }

            return PasswordVerificationResult.Failed;
        }

        private static bool VerifyAspNetCoreIdentityHash(string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrWhiteSpace(hashedPassword) || providedPassword == null)
            {
                return false;
            }

            byte[] decodedHashedPassword;
            try
            {
                decodedHashedPassword = Convert.FromBase64String(hashedPassword);
            }
            catch
            {
                return false;
            }

            if (decodedHashedPassword.Length == 0)
            {
                return false;
            }

            switch (decodedHashedPassword[0])
            {
                case 0x00:
                    return VerifyIdentityV2(decodedHashedPassword, providedPassword);
                case 0x01:
                    return VerifyIdentityV3(decodedHashedPassword, providedPassword);
                default:
                    return false;
            }
        }

        private static bool VerifyIdentityV2(byte[] hashedPassword, string password)
        {
            // Format: { 0x00, salt[16], subkey[32] }
            if (hashedPassword.Length != 49)
            {
                return false;
            }

            var salt = new byte[16];
            Buffer.BlockCopy(hashedPassword, 1, salt, 0, salt.Length);

            var storedSubkey = new byte[32];
            Buffer.BlockCopy(hashedPassword, 17, storedSubkey, 0, storedSubkey.Length);

            byte[] generatedSubkey;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, 1000, HashAlgorithmName.SHA1))
            {
                generatedSubkey = deriveBytes.GetBytes(32);
            }

            return ByteArraysEqual(storedSubkey, generatedSubkey);
        }

        private static bool VerifyIdentityV3(byte[] hashedPassword, string password)
        {
            try
            {
                var prf = ReadNetworkByteOrder(hashedPassword, 1);
                var iterCount = ReadNetworkByteOrder(hashedPassword, 5);
                var saltLength = (int)ReadNetworkByteOrder(hashedPassword, 9);

                if (saltLength < 16 || hashedPassword.Length < 13 + saltLength)
                {
                    return false;
                }

                var salt = new byte[saltLength];
                Buffer.BlockCopy(hashedPassword, 13, salt, 0, salt.Length);

                var storedSubkeyLength = hashedPassword.Length - 13 - saltLength;
                if (storedSubkeyLength < 16)
                {
                    return false;
                }

                var storedSubkey = new byte[storedSubkeyLength];
                Buffer.BlockCopy(hashedPassword, 13 + saltLength, storedSubkey, 0, storedSubkey.Length);

                var algorithm = ResolvePbkdf2Algorithm(prf);
                if (algorithm == null)
                {
                    return false;
                }

                byte[] generatedSubkey;
                using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, (int)iterCount, algorithm.Value))
                {
                    generatedSubkey = deriveBytes.GetBytes(storedSubkeyLength);
                }

                return ByteArraysEqual(storedSubkey, generatedSubkey);
            }
            catch
            {
                return false;
            }
        }

        private static HashAlgorithmName? ResolvePbkdf2Algorithm(uint prf)
        {
            switch (prf)
            {
                case 0:
                    return HashAlgorithmName.SHA1;
                case 1:
                    return HashAlgorithmName.SHA256;
                case 2:
                    return HashAlgorithmName.SHA512;
                default:
                    return null;
            }
        }

        private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
        {
            return ((uint)buffer[offset] << 24)
                 | ((uint)buffer[offset + 1] << 16)
                 | ((uint)buffer[offset + 2] << 8)
                 | buffer[offset + 3];
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            var areSame = true;
            for (var i = 0; i < a.Length; i++)
            {
                areSame &= (a[i] == b[i]);
            }

            return areSame;
        }
    }
}
