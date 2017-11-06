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
            //hashing parameters
            const int saltSize = 64;
            const int hashIterations = 10000;

            ICryptoService cryptoService = new PBKDF2();
            hash = cryptoService.Compute(pass, saltSize, hashIterations);
            salt = cryptoService.Salt;
        }
    }
}