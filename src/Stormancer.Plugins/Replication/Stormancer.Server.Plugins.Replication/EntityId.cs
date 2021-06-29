using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Replication
{
    /// <summary>
    /// Represents the id of an entity.
    /// </summary>
    public struct EntityId
    {
        private Guid _value;

        /// <summary>
        /// Creates a byte array representation of the object.
        /// </summary>
        /// <returns></returns>
        public byte[] ToByteArray() => _value.ToByteArray();

        private EntityId(Guid guid)
        {
            _value = guid;
        }
        private EntityId(byte[] rawValue)
        {
            _value = new Guid(rawValue);
        }

        /// <summary>
        /// Returns true if the <see cref="EntityId"/> is empty.
        /// </summary>
        /// <returns></returns>
        public bool IsEmpty()
        {
            return _value == Guid.Empty;
        }

        /// <summary>
        /// An empty <see cref="SessionId"/>
        /// </summary>
        public static EntityId Empty
        {
            get
            {
                return new EntityId();
            }
        }

        /// <summary>
        /// Generates an hashcode for the <see cref="EntityId"/>
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _value.GetHashCode();

        }

        public static bool operator ==(EntityId sId1, EntityId sId2)
        {
            return sId1.Equals(sId2);
        }

        public static bool operator !=(EntityId sId1, EntityId sId2)
        {
            return !sId1.Equals(sId2);
        }

        /// <summary>
        /// Evaluates if a <see cref="SessionId"/> object is equal to the current one.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is EntityId)
            {
                var other = (EntityId)obj;


                return other._value == _value;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a string representation of the session id.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return System.Convert.ToBase64String(ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Creates a new <see cref="SessionId"/>.
        /// </summary>
        /// <returns></returns>
        public static EntityId CreateNew()
        {
            return new EntityId(Guid.NewGuid());
        }

        /// <summary>
        /// Creates a <see cref="SessionId"/> from a string representation.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static EntityId From(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return new EntityId();
            }
            else
            {
                string incoming = sessionId.Replace('_', '/').Replace('-', '+');
                switch (sessionId.Length % 4)
                {
                    case 2: incoming += "=="; break;
                    case 3: incoming += "="; break;
                }
                return new EntityId(System.Convert.FromBase64String(incoming));
            }
        }
        /// <summary>
        /// Creates a <see cref="SessionId"/> object from a binary representation.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static EntityId From(byte[] sessionId)
        {

            if (sessionId == null)
            {
                return new EntityId();
            }
            if (sessionId.Length != 16)
            {
                throw new ArgumentException("EntityIds must be 16 bytes long");
            }
            return new EntityId(sessionId);
        }
    }

    namespace Protocol
    {
        internal class EntityIdSerialization : IMsgPackSerializationPlugin
        {
            public static void Pack(Packer packer, EntityId value, SerializationContext ctx)
            {
                packer.PackRaw(value.ToByteArray());

            }

            public static EntityId Unpack(Unpacker unpacker, SerializationContext ctx)
            {
                var data = unpacker.LastReadData;
                if (data.IsRaw)
                {
                    return EntityId.From(data.AsBinary());
                }
                else
                {
                    throw new NotSupportedException($"Failed to unpack {data} as EntityId");
                }

            }
        }


       
    }
}
