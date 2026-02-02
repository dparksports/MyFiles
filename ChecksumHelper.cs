using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileLister
{
    public static class ChecksumHelper
    {
        public static string CalculateSha256(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static void SaveChecksumFile(string targetFilePath)
        {
            try
            {
                var checksum = CalculateSha256(targetFilePath);
                File.WriteAllText(targetFilePath + ".sha256", checksum);
            }
            catch (Exception ex)
            {
                // Non-blocking error logging/notification could go here, 
                // but for now we'll just rethrow or verify if we want to swallow.
                throw new Exception("Failed to generate checksum file.", ex);
            }
        }

        public static bool VerifyChecksumFile(string targetFilePath)
        {
            var checksumFile = targetFilePath + ".sha256";
            if (!File.Exists(checksumFile))
            {
                throw new FileNotFoundException("Checksum file not found.", checksumFile);
            }

            var expectedChecksum = File.ReadAllText(checksumFile).Trim();
            var actualChecksum = CalculateSha256(targetFilePath);

            return string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase);
        }
    }
}
