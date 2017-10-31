using System;
using System.Text;
using SimpleCrypto;

namespace Synthesis.GuestService.Workflow.Utilities
{
    public static class PasswordUtility
    {
        /// <summary>
        ///     Method for taking in password and providing the one way hash and salt for it.
        /// </summary>
        /// <param name="pass"></param>
        /// <param name="hash"></param>
        /// <param name="salt"></param>
        public static void HashAndSalt(string pass, out string hash, out string salt)
        {
            const int saltSize = 64;
            const int hashIterations = 10000;

            ICryptoService cryptoService = new PBKDF2();
            hash = cryptoService.Compute(pass, saltSize, hashIterations);
            salt = cryptoService.Salt;
        }

        /// <summary>
        ///     Method for generating a new random password
        /// </summary>
        /// <param name="length">Desired length of the password to be returned</param>
        public static string GenerateRandomPassword(int length)
        {
            const string valid = @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890~!@#$%^&*()_+-={}|:<>?[]\;',./'";
            var res = new StringBuilder();
            var rnd = new Random();
            while (0 < length--)
            {
                res.Append(valid[rnd.Next(valid.Length)]);
            }

            return res.ToString();
        }
    }
}