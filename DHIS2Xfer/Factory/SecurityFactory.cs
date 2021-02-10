using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DHIS2Xfer.Factory
{
    /// <summary>
    /// Used to encrypt or decrypt a string using AES - implements best practices 
    /// including CBC method, ISO10126 padding and salts
    /// </summary>
    public class SecurityFactory
    {
        //Set up parameters for the AES encryption
        public const string SecurityKey = "proj-bala-nce007";
        private const int AesBlockByteSize = 128 / 8;
        private const int PasswordSaltByteSize = 128 / 8;
        private const int PasswordByteSize = 256 / 8;
        private const int PasswordIterationCount = 100_000;
        private const int MinimumEncryptedMessageByteSize =
            PasswordSaltByteSize // auth salt
            + PasswordSaltByteSize // key salt
            + AesBlockByteSize // IV
            + AesBlockByteSize; // cipher text min length

        private static readonly Encoding StringEncoding = Encoding.UTF8;
        private static readonly RandomNumberGenerator Random = RandomNumberGenerator.Create();

        /// <summary>
        /// AES Encryption method
        /// </summary>
        /// <param name="toEncrypt">The string to encrypt</param>
        /// <param name="password">The password to encrypt it with</param>
        /// <returns>A ciphertext string</returns>
        public static string Encrypt(string toEncrypt, string password)
        {
            //Create a random salt
            var keySalt = GenerateRandomBytes(PasswordSaltByteSize);
            //Generate a key from the supplied password and salt
            var key = GetKey(password, keySalt);
            //Generate a random IV
            var iv = GenerateRandomBytes(AesBlockByteSize);

            byte[] cipherText;
            //Initialise the AES object
            using (var aes = CreateAes())
            //Pass in the key and IV
            using (var encryptor = aes.CreateEncryptor(key, iv))
            {
                //Convert the string to encrypt to a byte array
                var plainText = StringEncoding.GetBytes(toEncrypt);
                //Encrypt the plain text
                cipherText = encryptor.TransformFinalBlock(plainText, 0, plainText.Length);
            }

            //Generate random salt
            var authKeySalt = GenerateRandomBytes(PasswordSaltByteSize);
            //Combine the items into a single array
            var result = MergeArrays(authKeySalt, keySalt, iv, cipherText);
 
            //Convert the resulting cyphertext byte array to a string and return it
            string strResult = Convert.ToBase64String(result);
            return strResult;
        }

        /// <summary>
        /// AES Decryption method
        /// </summary>
        /// <param name="strEncrypted">The data to decrypt (Byte array)</param>
        /// <param name="password">The password to decrypt it with</param>
        /// <returns>A plain text string</returns>
        public static string Decrypt(string strEncryptedData, string password)
        {
            byte[] encryptedData = Convert.FromBase64String(strEncryptedData);
            //Throw an error if the data is empty
            if (encryptedData is null || encryptedData.Length < MinimumEncryptedMessageByteSize)
            {
                throw new ArgumentException("Invalid length of encrypted data");
            }
            //get the authKeySalt from the encrypted data array
            var authKeySalt = encryptedData.AsSpan(0, PasswordSaltByteSize).ToArray();
            //get the keySalt from the encryted data array
            var keySalt = encryptedData.AsSpan(PasswordSaltByteSize, PasswordSaltByteSize).ToArray();
            //get the IV from the encrypted data array
            var iv = encryptedData.AsSpan(2 * PasswordSaltByteSize, AesBlockByteSize).ToArray();

            //Get the index of the start of the actual cipherText from the encrypted data array
            var cipherTextIndex = authKeySalt.Length + keySalt.Length + iv.Length;
            //Get the length of the cipherText
            var cipherTextLength = encryptedData.Length - cipherTextIndex;

            //Generate a key from the supplied password and salt
            var key = GetKey(password, keySalt);

            // decrypt
            //Initialise the AES object
            using (var aes = CreateAes())
            {
                //Pass in the key and IV
                using (var encryptor = aes.CreateDecryptor(key, iv))
                {
                    //Get the decrypted byte array
                    var decryptedBytes = encryptor.TransformFinalBlock(encryptedData, cipherTextIndex, cipherTextLength);
                    //Return the decrypted byte array as a string                                                                                                                          //Return the decrypted byte array as a string
                    return StringEncoding.GetString(decryptedBytes);
                }
            }
        }

        /// <summary>
        /// Accepts any string and salts+hashes it, returning the string digest
        /// </summary>
        /// <param name="inString">input string</param>
        /// <returns>hash in string format</returns>
        public static string hashString (string inString, string saltStr)
        {
            byte[] salt = Convert.FromBase64String(saltStr);
            byte[] resultByte=GetKey(inString, salt);
            string result = Convert.ToBase64String(resultByte, 0, resultByte.Length);
            return result;
        }

        /// <summary>
        /// Used by encrypt and decrypt methods to initialize the AES object with the chosen parameters
        /// </summary>
        /// <returns>System.Security.Cryptography.Aes object</returns>
        private static Aes CreateAes()
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.ISO10126;
            return aes;
        }

        /// <summary>
        /// Generates a key from the supplied password
        /// </summary>
        /// <param name="password">The password string</param>
        /// <param name="passwordSalt">The password salt byte array</param>
        /// <returns>An AES256 hash to be used as an Aes key</returns>
        private static byte[] GetKey(string password, byte[] passwordSalt)
        {
            //convert password string to byte array
            var keyBytes = StringEncoding.GetBytes(password);
            //Generate a SHA256 derivitive of the password and salt
            using (var derivator = new Rfc2898DeriveBytes(keyBytes, passwordSalt, PasswordIterationCount, HashAlgorithmName.SHA256))
            {
                //return a SHA256 derivative of the password and salt as the AES key
                return derivator.GetBytes(PasswordByteSize);
            }
        }

        /// <summary>
        /// Used for generating random salts and IV
        /// </summary>
        /// <param name="numberOfBytes">The number of bytes to generate</param>
        /// <returns>A random byte array</returns>
        private static byte[] GenerateRandomBytes(int numberOfBytes)
        {
            var randomBytes = new byte[numberOfBytes];
            Random.GetBytes(randomBytes);
            return randomBytes;
        }

        /// <summary>
        /// Used to merge all encrypted byte arrays into one (salts, IV, ciphertext) prior to output of the final encrypted byte array
        /// </summary>
        /// <param name="additionalCapacity">Used to store the additional capacity required in the array</param>
        /// <param name="arrays">The byte arrays that need to be combined</param>
        /// <returns>A byte array of all combined items</returns>
        private static byte[] MergeArrays(params byte[][] arrays)

        {
            var merged = new byte[arrays.Sum(a => a.Length)];
            var mergeIndex = 0;
            for (int i = 0; i < arrays.GetLength(0); i++)
            {
                arrays[i].CopyTo(merged, mergeIndex);
                mergeIndex += arrays[i].Length;
            }

            return merged;
        }

    }
}
