using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSmith.Core
{
    /// <summary>
    /// Represents a globally unique identifier (GUID) with a shorter string value.
    /// </summary>
    public struct ShortGuid : IComparable, IComparable<Guid>, IEquatable<Guid>, IComparable<ShortGuid>, IEquatable<ShortGuid>
    {
        /// <summary>
        /// A read-only instance of the ShortGuid class whose value 
        /// is guaranteed to be all zeroes. 
        /// </summary>
        public static readonly ShortGuid Empty = new ShortGuid(Guid.Empty);
        
        /// <summary>
        /// Creates a ShortGuid from a base64 encoded string
        /// </summary>
        /// <param name="value">The encoded guid as a 
        /// base64 string</param>
        public ShortGuid(string value)
        {
            _value = value;
            _guid = Decode(value);
        }

        /// <summary>
        /// Creates a ShortGuid from a Guid
        /// </summary>
        /// <param name="guid">The Guid to encode</param>
        public ShortGuid(Guid guid)
        {
            _value = Encode(guid);
            _guid = guid;
        }

        private Guid _guid;
        
        /// <summary>
        /// Gets/sets the underlying Guid
        /// </summary>
        public Guid Guid
        {
            get { return _guid; }
            set
            {
                if (value != _guid)
                {
                    _guid = value;
                    _value = Encode(value);
                }
            }
        }

        string _value;

        /// <summary>
        /// Gets/sets the underlying base64 encoded string
        /// </summary>
        public string Value
        {
            get { return _value; }
            set
            {
                if (value != _value)
                {
                    _value = value;
                    _guid = Decode(value);
                }
            }
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared.
        /// </returns>
        /// <param name="obj">
        /// An object to compare with this instance. 
        /// </param>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            if (obj is ShortGuid)
                return _guid.CompareTo(((ShortGuid)obj)._guid);
            if (obj is Guid)
                return _guid.CompareTo((Guid)obj);
            if (obj is string)
                return _guid.CompareTo(((ShortGuid)obj)._guid);
            return 0;            
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared.
        /// </returns>
        /// <param name="other">
        /// An object to compare with this instance. 
        /// </param>
        public int CompareTo(Guid other)
        {
            return _guid.CompareTo(other);
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that indicates the relative order of the objects being compared.
        /// </returns>
        /// <param name="other">
        /// An object to compare with this instance. 
        /// </param>
        public int CompareTo(ShortGuid other)
        {
            return _guid.CompareTo(other._guid);
        }

        /// <summary>
        /// Returns the base64 encoded guid as a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _value;
        }

        /// <summary>
        /// Returns a value indicating whether this instance and a
        /// specified Object represent the same type and value.
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (obj is ShortGuid)
                return _guid.Equals(((ShortGuid)obj)._guid);
            if (obj is Guid)
                return _guid.Equals((Guid)obj);
            if (obj is string)
                return _guid.Equals(((ShortGuid)obj)._guid);
            return false;
        }
        
        /// <summary>
        /// Returns a value indicating whether this instance and a
        /// specified Object represent the same type and value.
        /// </summary>
        /// <param name="other">The object to compare</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public bool Equals(ShortGuid other)
        {
            return _guid.Equals(other._guid);
        }

        /// <summary>
        /// Returns a value indicating whether this instance and a
        /// specified Object represent the same type and value.
        /// </summary>
        /// <param name="other">The object to compare</param>
        /// <returns>
        /// 	<c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public bool Equals(Guid other)
        {
            return _guid.Equals(other);
        }

        /// <summary>
        /// Returns the HashCode for underlying Guid.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }
        
        /// <summary>
        /// Initialises a new instance of the ShortGuid class
        /// </summary>
        /// <returns></returns>
        public static ShortGuid NewGuid()
        {
            return new ShortGuid(Guid.NewGuid());
        }

        /// <summary>
        /// Creates a new instance of a Guid using the string value, 
        /// then returns the base64 encoded version of the Guid.
        /// </summary>
        /// <param name="value">An actual Guid string (i.e. not a ShortGuid)</param>
        /// <returns></returns>
        public static string Encode(string value)
        {
            Guid guid = new Guid(value);
            return Encode(guid);
        }

        /// <summary>
        /// Encodes the given Guid as a base64 string that is 22 
        /// characters long.
        /// </summary>
        /// <param name="guid">The Guid to encode</param>
        /// <returns></returns>
        public static string Encode(Guid guid)
        {
            string encoded = Convert.ToBase64String(guid.ToByteArray());
            encoded = encoded.Substring(0, 22);
            return EncodeBase62(encoded);
        }

        private static string EncodeBase62(string base64)
        {
            var buf = new StringBuilder(base64.Length * 2);

            for (int i = 0; i < base64.Length; i++)
            {
                char ch = base64[i];
                switch (ch)
                {
                    case 'i':
                        buf.Append("ii");
                        break;

                    case '+':
                        buf.Append("ip");
                        break;

                    case '/':
                        buf.Append("is");
                        break;

                    case '=':
                        buf.Append("ie");
                        break;

                    case '\n':
                        // Strip out
                        break;

                    default:
                        buf.Append(ch);
                        break;
                }
            }
            return buf.ToString();
        }

        /// <summary>
        /// Decodes the given base64 string
        /// </summary>
        /// <param name="value">The base64 encoded string of a Guid</param>
        /// <returns>A new Guid</returns>
        public static Guid Decode(string value)
        {
            value = DecodeBase62(value);
            byte[] buffer = Convert.FromBase64String(value + "==");
            return new Guid(buffer);
        }

        private static string DecodeBase62(string base62)
        {
            var buf = new StringBuilder(base62.Length);

            int i = 0;
            while (i < base62.Length)
            {
                char ch = base62[i];

                if (ch == 'i')
                {
                    i++;
                    char code = base62[i];
                    switch (code)
                    {
                        case 'i':
                            buf.Append('i');
                            break;

                        case 'p':
                            buf.Append('+');
                            break;

                        case 's':
                            buf.Append('/');
                            break;

                        case 'e':
                            buf.Append('=');
                            break;
                    }
                }
                else
                {
                    buf.Append(ch);
                }

                i++;
            }
            return buf.ToString();
        }

        /// <summary>
        /// Determines if both ShortGuids have the same underlying 
        /// Guid value.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator ==(ShortGuid x, ShortGuid y)
        {
            if ((object)x == null) return (object)y == null;
            return x._guid == y._guid;
        }

        /// <summary>
        /// Determines if both ShortGuids do not have the 
        /// same underlying Guid value.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator !=(ShortGuid x, ShortGuid y)
        {
            return !(x == y);
        }

        /// <summary>
        /// Implicitly converts the ShortGuid to it's string equivilent
        /// </summary>
        /// <param name="shortGuid"></param>
        /// <returns></returns>
        public static implicit operator string(ShortGuid shortGuid)
        {
            return shortGuid._value;
        }

        /// <summary>
        /// Implicitly converts the ShortGuid to it's Guid equivilent
        /// </summary>
        /// <param name="shortGuid"></param>
        /// <returns></returns>
        public static implicit operator Guid(ShortGuid shortGuid)
        {
            return shortGuid._guid;
        }

        /// <summary>
        /// Implicitly converts the string to a ShortGuid
        /// </summary>
        /// <param name="shortGuid"></param>
        /// <returns></returns>
        public static implicit operator ShortGuid(string shortGuid)
        {
            return new ShortGuid(shortGuid);
        }

        /// <summary>
        /// Implicitly converts the Guid to a ShortGuid 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static implicit operator ShortGuid(Guid guid)
        {
            return new ShortGuid(guid);
        }
    }
}
