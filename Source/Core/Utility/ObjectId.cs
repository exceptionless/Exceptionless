using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Utility {
    public struct ObjectId : IComparable<ObjectId>, IEquatable<ObjectId>, IConvertible {
        private static readonly ObjectId __emptyInstance = default(ObjectId);
        private static readonly int __staticMachine;
        private static readonly short __staticPid;
        private static int __staticIncrement;
        private static DateTime __unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly int _timestamp;
        private readonly int _machine;
        private readonly short _pid;
        private readonly int _increment;

        static ObjectId() {
            __staticMachine = (GetMachineHash() + AppDomain.CurrentDomain.Id) & 0x00ffffff;
            __staticIncrement = (new Random()).Next();

            try {
                __staticPid = (short)GetCurrentProcessId();
            } catch (SecurityException) {
                __staticPid = 0;
            }
        }

        public ObjectId(byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            Unpack(bytes, out _timestamp, out _machine, out _pid, out _increment);
        }

        internal ObjectId(byte[] bytes, int index) {
            _timestamp = (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
            _machine = (bytes[index + 4] << 16) | (bytes[index + 5] << 8) | bytes[index + 6];
            _pid = (short)((bytes[index + 7] << 8) | bytes[index + 8]);
            _increment = (bytes[index + 9] << 16) | (bytes[index + 10] << 8) | bytes[index + 11];
        }

        public ObjectId(DateTime timestamp, int machine, short pid, int increment) : this(GetTimestampFromDateTime(timestamp), machine, pid, increment) {}

        public ObjectId(int timestamp, int machine, short pid, int increment) {
            if ((machine & 0xff000000) != 0) {
                throw new ArgumentOutOfRangeException(nameof(machine), "The machine value must be between 0 and 16777215 (it must fit in 3 bytes).");
            }
            if ((increment & 0xff000000) != 0) {
                throw new ArgumentOutOfRangeException(nameof(increment), "The increment value must be between 0 and 16777215 (it must fit in 3 bytes).");
            }

            _timestamp = timestamp;
            _machine = machine;
            _pid = pid;
            _increment = increment;
        }

        public ObjectId(string value) {
            if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }
            Unpack(Utils.ParseHexString(value), out _timestamp, out _machine, out _pid, out _increment);
        }

        public static ObjectId Empty => __emptyInstance;

        public int Timestamp => _timestamp;

        public int Machine => _machine;

        public short Pid => _pid;

        public int Increment => _increment;

        public DateTime CreationTime => __unixEpoch.AddSeconds(_timestamp);

        public static bool operator <(ObjectId lhs, ObjectId rhs) {
            return lhs.CompareTo(rhs) < 0;
        }

        public static bool operator <=(ObjectId lhs, ObjectId rhs) {
            return lhs.CompareTo(rhs) <= 0;
        }

        public static bool operator ==(ObjectId lhs, ObjectId rhs) {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ObjectId lhs, ObjectId rhs) {
            return !(lhs == rhs);
        }

        public static bool operator >=(ObjectId lhs, ObjectId rhs) {
            return lhs.CompareTo(rhs) >= 0;
        }

        public static bool operator >(ObjectId lhs, ObjectId rhs) {
            return lhs.CompareTo(rhs) > 0;
        }

        public static ObjectId GenerateNewId() {
            return GenerateNewId(GetTimestampFromDateTime(DateTime.UtcNow));
        }

        public static ObjectId GenerateNewId(DateTime timestamp) {
            return GenerateNewId(GetTimestampFromDateTime(timestamp));
        }

        public static ObjectId GenerateNewId(int timestamp) {
            int increment = Interlocked.Increment(ref __staticIncrement) & 0x00ffffff; // only use low order 3 bytes
            return new ObjectId(timestamp, __staticMachine, __staticPid, increment);
        }

        public static byte[] Pack(int timestamp, int machine, short pid, int increment) {
            if ((machine & 0xff000000) != 0) {
                throw new ArgumentOutOfRangeException(nameof(machine), "The machine value must be between 0 and 16777215 (it must fit in 3 bytes).");
            }
            if ((increment & 0xff000000) != 0) {
                throw new ArgumentOutOfRangeException(nameof(increment), "The increment value must be between 0 and 16777215 (it must fit in 3 bytes).");
            }

            byte[] bytes = new byte[12];
            bytes[0] = (byte)(timestamp >> 24);
            bytes[1] = (byte)(timestamp >> 16);
            bytes[2] = (byte)(timestamp >> 8);
            bytes[3] = (byte)(timestamp);
            bytes[4] = (byte)(machine >> 16);
            bytes[5] = (byte)(machine >> 8);
            bytes[6] = (byte)(machine);
            bytes[7] = (byte)(pid >> 8);
            bytes[8] = (byte)(pid);
            bytes[9] = (byte)(increment >> 16);
            bytes[10] = (byte)(increment >> 8);
            bytes[11] = (byte)(increment);
            return bytes;
        }

        public static ObjectId Parse(string s) {
            if (s == null) {
                throw new ArgumentNullException(nameof(s));
            }
            ObjectId objectId;
            if (TryParse(s, out objectId)) {
                return objectId;
            } else {
                var message = $"'{s}' is not a valid 24 digit hex string.";
                throw new FormatException(message);
            }
        }

        public static bool TryParse(string s, out ObjectId objectId) {
            if (s != null && s.Length == 24) {
                byte[] bytes;
                if (Utils.TryParseHexString(s, out bytes)) {
                    objectId = new ObjectId(bytes);
                    return true;
                }
            }

            objectId = default(ObjectId);
            return false;
        }

        public static void Unpack(byte[] bytes, out int timestamp, out int machine, out short pid, out int increment) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (bytes.Length != 12) {
                throw new ArgumentOutOfRangeException(nameof(bytes), "Byte array must be 12 bytes long.");
            }
            timestamp = (bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3];
            machine = (bytes[4] << 16) + (bytes[5] << 8) + bytes[6];
            pid = (short)((bytes[7] << 8) + bytes[8]);
            increment = (bytes[9] << 16) + (bytes[10] << 8) + bytes[11];
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetCurrentProcessId() {
            return Process.GetCurrentProcess().Id;
        }

        private static int GetMachineHash() {
            var hostName = Environment.MachineName;
            return 0x00ffffff & hostName.GetHashCode();
        }

        private static int GetTimestampFromDateTime(DateTime timestamp) {
            var secondsSinceEpoch = (long)Math.Floor((Utils.ToUniversalTime(timestamp) - __unixEpoch).TotalSeconds);
            if (secondsSinceEpoch < int.MinValue || secondsSinceEpoch > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(timestamp));
            }
            return (int)secondsSinceEpoch;
        }

        public int CompareTo(ObjectId other) {
            int r = _timestamp.CompareTo(other._timestamp);
            if (r != 0) {
                return r;
            }
            r = _machine.CompareTo(other._machine);
            if (r != 0) {
                return r;
            }
            r = _pid.CompareTo(other._pid);
            if (r != 0) {
                return r;
            }
            return _increment.CompareTo(other._increment);
        }

        public bool Equals(ObjectId rhs) {
            return _timestamp == rhs._timestamp && _machine == rhs._machine && _pid == rhs._pid && _increment == rhs._increment;
        }

        public override bool Equals(object obj) {
            if (obj is ObjectId) {
                return Equals((ObjectId)obj);
            } else {
                return false;
            }
        }

        public override int GetHashCode() {
            int hash = 17;
            hash = 37 * hash + _timestamp.GetHashCode();
            hash = 37 * hash + _machine.GetHashCode();
            hash = 37 * hash + _pid.GetHashCode();
            hash = 37 * hash + _increment.GetHashCode();
            return hash;
        }

        public byte[] ToByteArray() {
            return Pack(_timestamp, _machine, _pid, _increment);
        }

        public override string ToString() {
            return Pack(_timestamp, _machine, _pid, _increment).ToHex();
        }

        internal void GetBytes(byte[] bytes, int index) {
            bytes[index] = (byte)(_timestamp >> 24);
            bytes[1 + index] = (byte)(_timestamp >> 16);
            bytes[2 + index] = (byte)(_timestamp >> 8);
            bytes[3 + index] = (byte)(_timestamp);
            bytes[4 + index] = (byte)(_machine >> 16);
            bytes[5 + index] = (byte)(_machine >> 8);
            bytes[6 + index] = (byte)(_machine);
            bytes[7 + index] = (byte)(_pid >> 8);
            bytes[8 + index] = (byte)(_pid);
            bytes[9 + index] = (byte)(_increment >> 16);
            bytes[10 + index] = (byte)(_increment >> 8);
            bytes[11 + index] = (byte)(_increment);
        }

        TypeCode IConvertible.GetTypeCode() {
            return TypeCode.Object;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        byte IConvertible.ToByte(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        char IConvertible.ToChar(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        double IConvertible.ToDouble(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        short IConvertible.ToInt16(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        int IConvertible.ToInt32(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        long IConvertible.ToInt64(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        float IConvertible.ToSingle(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        string IConvertible.ToString(IFormatProvider provider) {
            return ToString();
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider provider) {
            switch (Type.GetTypeCode(conversionType)) {
                case TypeCode.String:
                    return ((IConvertible)this).ToString(provider);
                case TypeCode.Object:
                    if (conversionType == typeof(object) || conversionType == typeof(ObjectId)) {
                        return this;
                    }
                    break;
            }

            throw new InvalidCastException();
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        uint IConvertible.ToUInt32(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider) {
            throw new InvalidCastException();
        }

        internal static class Utils {

            public static byte[] ParseHexString(string s) {
                if (s == null) {
                    throw new ArgumentNullException(nameof(s));
                }

                byte[] bytes;
                if ((s.Length & 1) != 0) {
                    s = "0" + s; // make length of s even
                }
                bytes = new byte[s.Length / 2];
                for (int i = 0; i < bytes.Length; i++) {
                    string hex = s.Substring(2 * i, 2);
                    try {
                        byte b = Convert.ToByte(hex, 16);
                        bytes[i] = b;
                    } catch (FormatException e) {
                        throw new FormatException($"Invalid hex string {s}. Problem with substring {hex} starting at position {2 * i}", e);
                    }
                }

                return bytes;
            }

            public static string ToHexString(byte[] bytes) {
                if (bytes == null) {
                    throw new ArgumentNullException(nameof(bytes));
                }
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) {
                    sb.AppendFormat("{0:x2}", b);
                }
                return sb.ToString();
            }

            public static DateTime ToUniversalTime(DateTime dateTime) {
                if (dateTime == DateTime.MinValue) {
                    return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
                } else if (dateTime == DateTime.MaxValue) {
                    return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
                } else {
                    return dateTime.ToUniversalTime();
                }
            }

            public static bool TryParseHexString(string s, out byte[] bytes) {
                try {
                    bytes = ParseHexString(s);
                } catch {
                    bytes = null;
                    return false;
                }

                return true;
            }
        }
    }
}