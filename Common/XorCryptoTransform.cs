
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
        }

        public bool CanReuseTransform => true;

        public bool CanTransformMultipleBlocks => true;

        public int InputBlockSize => 1;  // Process data byte by byte (can be optimized to larger block size)

        public int OutputBlockSize => 1;

        // Performs the XOR operation on a block of data
        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            // Validate input parameters
            if (inputBuffer == null) throw new ArgumentNullException(nameof(inputBuffer));
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));
            if (inputOffset < 0 || inputOffset + inputCount > inputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(inputOffset), "Invalid input offset or count.");
            if (outputOffset < 0 || outputOffset + inputCount > outputBuffer.Length)
                throw new ArgumentOutOfRangeException(nameof(outputOffset), "Invalid output offset.");

            // Perform XOR encryption/decryption
            for (int i = 0; i < inputCount; i++)
            {
                outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ key[i % key.Length]);
            }

            return inputCount;
        }

        // Final block transformation (in this case, just calls TransformBlock since XOR doesn't need padding)
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
