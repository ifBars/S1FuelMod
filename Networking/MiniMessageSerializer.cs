using System;
using System.Text;

namespace S1FuelMod.Networking
{
    /// <summary>
    /// Lightweight message serializer patterned after SteamNetworkLib.Utilities.MessageSerializer.
    /// Format: ["SNLM"][1 byte typeLen][type UTF8][payload UTF8 JSON]
    /// </summary>
    internal static class MiniMessageSerializer
    {
        public const string HEADER = "SNLM"; // reuse same 4-byte header for easy sniffing

        public static byte[] SerializeMessage(MiniP2PMessage message)
        {
            try
            {
                var payload = Encoding.UTF8.GetBytes(message.SerializeJson());
                var typeBytes = Encoding.UTF8.GetBytes(message.MessageType);
                var headerBytes = Encoding.UTF8.GetBytes(HEADER);

                // Validate type length fits in byte
                if (typeBytes.Length > 255)
                {
                    throw new Exception($"MiniMessageSerializer: Message type too long: {typeBytes.Length}");
                }

                int total = headerBytes.Length + 1 + typeBytes.Length + payload.Length;
                var data = new byte[total];
                int offset = 0;
                Array.Copy(headerBytes, 0, data, offset, headerBytes.Length); offset += headerBytes.Length;
                data[offset++] = (byte)typeBytes.Length;
                Array.Copy(typeBytes, 0, data, offset, typeBytes.Length); offset += typeBytes.Length;
                Array.Copy(payload, 0, data, offset, payload.Length);
                return data;
            }
            catch (Exception ex)
            {
                throw new Exception($"MiniMessageSerializer: SerializeMessage failed for {message.MessageType}", ex);
            }
        }

        public static bool IsValidMessage(byte[] data)
        {
            if (data == null || data.Length < 6) return false;
            var header = Encoding.UTF8.GetBytes(HEADER);
            for (int i = 0; i < header.Length; i++)
            {
                if (data[i] != header[i]) return false;
            }
            int typeLen = data[header.Length];
            return header.Length + 1 + typeLen <= data.Length;
        }

        public static string? GetMessageType(byte[] data)
        {
            if (!IsValidMessage(data)) return null;
            try
            {
                var header = Encoding.UTF8.GetBytes(HEADER);
                int offset = header.Length;
                int typeLen = data[offset++];
                if (offset + typeLen > data.Length) return null;
                return Encoding.UTF8.GetString(data, offset, typeLen);
            }
            catch (Exception)
            {
                // UTF8 decoding can fail in IL2CPP
                return null;
            }
        }

        public static T CreateMessage<T>(byte[] data) where T : MiniP2PMessage, new()
        {
            // Validate message format first
            if (!IsValidMessage(data))
            {
                throw new Exception("MiniMessageSerializer: Invalid message format in CreateMessage");
            }
            
            var header = Encoding.UTF8.GetBytes(HEADER);
            int offset = header.Length;
            int typeLen = data[offset++];
            offset += typeLen;
            int payloadLen = data.Length - offset;
            
            // Ensure we don't go out of bounds
            if (payloadLen < 0)
            {
                throw new Exception("MiniMessageSerializer: Invalid payload length");
            }
            
            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(data, offset, payloadLen);
            }
            catch (Exception ex)
            {
                throw new Exception("MiniMessageSerializer: UTF8 decoding failed for payload", ex);
            }
            var msg = new T();
            var actualType = GetMessageType(data);
            if (msg.MessageType != actualType)
            {
                throw new Exception($"MiniMessageSerializer: type mismatch expected {msg.MessageType}, got {actualType}");
            }
            msg.DeserializeJson(payload);
            return msg;
        }
    }
}


