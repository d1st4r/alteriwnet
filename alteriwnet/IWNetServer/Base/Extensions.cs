using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IWNetServer
{
    public static class Extensions
    {
        public static bool IsAtEOF(this Stream stream)
        {
            return (stream.Position >= stream.Length);
        }

        public static string ReadNullString(this BinaryReader reader)
        {
            string retval = "";
            char[] buffer = new char[1];
            buffer[0] = '\xFF';

            while (true)
            {
                reader.Read(buffer, 0, 1);

                if (buffer[0] == 0)
                {
                    break;
                }

                retval += buffer[0];
            }

            return retval;
        }

        public static string ReadFixedString(this BinaryReader reader, int length)
        {
            byte[] buffer = new byte[length];
            reader.Read(buffer, 0, length);

            string retval = Encoding.ASCII.GetString(buffer);
            return retval.Trim('\0');
        }
    }
}
