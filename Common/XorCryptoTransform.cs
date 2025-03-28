namespace Kzone.Signal.Common
{
    using System;
    using System.Security.Cryptography;

    internal class XorCryptoTransform : ICryptoTransform, IDisposable
    {
        private readonly byte[] key;
        private bool disposedValue;

        // Constructor that initializes the XOR key
        public XorCryptoTransform(byte[] key)
        {
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("Key cannot be empty.", nameof(key));

            // Validate that the key is exactly 3 bytes
            if (key.Length != 3)
                throw new ArgumentException("Key must be exactly 3 bytes in length.", nameof(key));
        }

        public bool CanReuseTransform => true;
        public bool CanTransformMultipleBlocks => true;
        public int InputBlockSize => 1;  // Keep original value for compatibility
        public int OutputBlockSize => 1;

        // XOR operation optimized for a fixed 3-byte key
        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            // Validate input parameters
            if (inputBuffer == null) throw new ArgumentNullException(nameof(inputBuffer));
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));
            if (inputOffset < 0 || inputOffset + inputCount > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputOffset), "Invalid input offset or count.");
            if (outputOffset < 0 || outputOffset + inputCount > outputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(outputOffset), "Invalid output offset.");

            // Extract key bytes for faster access
            byte key0 = key[0], key1 = key[1], key2 = key[2];

            int i = 0;

            // Process in blocks of 3 bytes to match the key pattern
            while (i + 3 <= inputCount)
            {
                outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ key0);
                outputBuffer[outputOffset + i + 1] = (byte)(inputBuffer[inputOffset + i + 1] ^ key1);
                outputBuffer[outputOffset + i + 2] = (byte)(inputBuffer[inputOffset + i + 2] ^ key2);
                i += 3;
            }

            // Handle remaining bytes (if any)
            if (i < inputCount)
                outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ key0);
            if (i + 1 < inputCount)
                outputBuffer[outputOffset + i + 1] = (byte)(inputBuffer[inputOffset + i + 1] ^ key1);

            return inputCount;
        }

        // Final block transformation
        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            if (inputBuffer == null) throw new ArgumentNullException(nameof(inputBuffer));
            if (inputOffset < 0 || inputOffset + inputCount > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputOffset), "Invalid input offset or count.");

            byte[] outputBuffer = new byte[inputCount];
            TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, 0);
            return outputBuffer;
        }

        // Dispose pattern to clean up resources and key
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Clear the key to prevent key leakage in memory
                    if (key != null)
                    {
                        Array.Clear(key, 0, key.Length);
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}