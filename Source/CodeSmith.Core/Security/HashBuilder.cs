using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Security
{
    public class HashBuilder : DisposableBase
    {
        public HashBuilder()
        {
            BufferStream = new MemoryStream();
            Writer = new BinaryWriter(BufferStream, Encoding.Unicode);
        }

        public HashAlgorithm HashAlgorithm { get; set; }
        
        protected BinaryWriter Writer { get; set; }
        
        protected MemoryStream BufferStream { get; set; }

        public void Append(bool value)
        {
            Writer.Write(value);
        }

        public void Append(byte value)
        {
            Writer.Write(value);
        }

        public void Append(char value)
        {
            Writer.Write(value);
        }

        public void Append(decimal value)
        {
#if SILVERLIGHT
            var bits = decimal.GetBits(value);            
            var buffer = new byte[16];

            buffer[0] = (byte)bits[0];
            buffer[1] = (byte)(bits[0] >> 8);
            buffer[2] = (byte)(bits[0] >> 16);
            buffer[3] = (byte)(bits[0] >> 24);
            buffer[4] = (byte)bits[1];
            buffer[5] = (byte)(bits[1] >> 8);
            buffer[6] = (byte)(bits[1] >> 16);
            buffer[7] = (byte)(bits[1] >> 24);
            buffer[8] = (byte)bits[2];
            buffer[9] = (byte)(bits[2] >> 8);
            buffer[10] = (byte)(bits[2] >> 16);
            buffer[11] = (byte)(bits[2] >> 24);
            buffer[12] = (byte)bits[3];
            buffer[13] = (byte)(bits[3] >> 8);
            buffer[14] = (byte)(bits[3] >> 16);
            buffer[15] = (byte)(bits[3] >> 24);

            Writer.Write(buffer, 0, 16);
#else
            Writer.Write(value);
#endif
        }

        public void Append(double value)
        {
            Writer.Write(value);
        }

        public void Append(short value)
        {
            Writer.Write(value);
        }

        public void Append(int value)
        {
            Writer.Write(value);
        }

        public void Append(long value)
        {
            Writer.Write(value);
        }

        public void Append(float value)
        {
            Writer.Write(value);
        }

        public void Append(string value)
        {
            if (value.IsNullOrEmpty())
                return;

            Writer.Write(value);
        }

        public void Append(DateTime value)
        {
            var binary = value.ToBinary();
            Writer.Write(binary);
        }

        public void Append(Guid value)
        {
            byte[] buffer = value.ToByteArray();
            Writer.Write(buffer);
        }

        public string GetHash()
        {
            byte[] hashData = ComputeHash(); 
            return hashData.ToHex();
        }

        public byte[] ComputeHash()
        {
#if SILVERLIGHT
            if (HashAlgorithm == null)
                HashAlgorithm = new SHA1Managed();
#else
            if (HashAlgorithm == null)
                HashAlgorithm = SHA1.Create();            
#endif
            Writer.Flush();

            byte[] allData = BufferStream.ToArray();
            return HashAlgorithm.ComputeHash(allData);
        }

        public override string ToString()
        {
            return GetHash();
        }

        protected override void DisposeManagedResources()
        {
            ((IDisposable)Writer).Dispose();
            ((IDisposable)BufferStream).Dispose();
        }
    }
}