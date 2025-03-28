
using System.Text;

namespace Kzone.Signal.Extensions
{
    public class CustomRandom
    {
        public static int RandomNumber(int min, int max) => new System.Random().Next(min, max);

        public static string RandomString(int size, bool lowerCase = false)
        {
            var builder = new StringBuilder(size);
            char offset = lowerCase ? 'a' : 'A';
            const int lettersOffset = 26;

            for (var i = 0; i < size; i++)
            {
                var @char = (char)new System.Random().Next(offset, offset + lettersOffset);
                builder.Append(@char);
            }

            return lowerCase ? builder.ToString().ToLower() : builder.ToString();
        }
    }
}
