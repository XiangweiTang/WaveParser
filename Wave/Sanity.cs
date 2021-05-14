using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wave
{
    public static class Sanity
    {
        public static void Requires(bool valid, string message = "")
        {
            if (!valid)
                throw new WaveException(message);
        }
    }

    public class WaveException : Exception
    {
        public WaveException() : base() { }
        public WaveException(string message) : base(message) { }
    }
}
