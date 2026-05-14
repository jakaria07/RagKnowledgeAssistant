using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class MockEmbeddingService
{
    private const int EmbeddingDimension = 768;

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(Array.Empty<float>());

        var vector = new float[EmbeddingDimension];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        for (int i = 0; i < EmbeddingDimension; i++)
        {
            var byteIndex = i % hash.Length;
            var nextByteIndex = (i + 1) % hash.Length;
            var combined = (hash[byteIndex] << 8) | hash[nextByteIndex];
            vector[i] = combined / 32768.0f;
        }

        return Task.FromResult(vector);
    }
}
