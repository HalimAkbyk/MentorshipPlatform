namespace MentorshipPlatform.Infrastructure.Services;

using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Agora AccessToken (006) builder — compatible with Agora Web SDK NG (v4).
/// Based on the official algorithm from github.com/AgoraIO/Tools.
/// </summary>
public static class AgoraTokenBuilder
{
    private const ushort KJoinChannel = 1;
    private const ushort KPublishAudioStream = 2;
    private const ushort KPublishVideoStream = 3;
    private const ushort KPublishDataStream = 4;

    /// <summary>
    /// Build an RTC token for the given channel.
    /// Use uid = "0" for wildcard (any uid can use the token).
    /// Use uid = "12345" for a specific numeric uid.
    /// </summary>
    public static string BuildToken(
        string appId,
        string appCertificate,
        string channelName,
        string uid,
        uint privilegeExpiredTs)
    {
        var msg = new Message
        {
            Salt = (uint)Random.Shared.Next(1, 99999999),
            Ts = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        msg.Privileges[KJoinChannel] = privilegeExpiredTs;
        msg.Privileges[KPublishAudioStream] = privilegeExpiredTs;
        msg.Privileges[KPublishVideoStream] = privilegeExpiredTs;
        msg.Privileges[KPublishDataStream] = privilegeExpiredTs;

        var msgBytes = msg.Pack();

        // Signature: two-round HMAC — HMAC(HMAC(cert, appId), msgBytes)
        var signKey = HmacSign(Encoding.UTF8.GetBytes(appCertificate), Encoding.UTF8.GetBytes(appId));
        var signature = HmacSign(signKey, msgBytes);

        // CRC32 of channelName and uid
        var crcChannel = ComputeCrc32(Encoding.UTF8.GetBytes(channelName));
        var crcUid = ComputeCrc32(Encoding.UTF8.GetBytes(uid));

        // Build content: appId + signature + msgBytes + crc32(channel) + crc32(uid)
        using var ms = new MemoryStream();
        WriteString(ms, appId);
        WriteBytes(ms, signature);
        WriteBytes(ms, msgBytes);
        WriteUint32(ms, crcChannel);
        WriteUint32(ms, crcUid);

        return "006" + Convert.ToBase64String(ms.ToArray());
    }

    private static byte[] HmacSign(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private static uint ComputeCrc32(byte[] data)
    {
        return Crc32.HashToUInt32(data);
    }

    private static void WriteString(Stream s, string val)
    {
        var bytes = Encoding.UTF8.GetBytes(val);
        WriteUint16(s, (ushort)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static void WriteBytes(Stream s, byte[] val)
    {
        WriteUint16(s, (ushort)val.Length);
        s.Write(val, 0, val.Length);
    }

    private static void WriteUint16(Stream s, ushort val)
    {
        s.WriteByte((byte)(val & 0xFF));
        s.WriteByte((byte)((val >> 8) & 0xFF));
    }

    private static void WriteUint32(Stream s, uint val)
    {
        s.WriteByte((byte)(val & 0xFF));
        s.WriteByte((byte)((val >> 8) & 0xFF));
        s.WriteByte((byte)((val >> 16) & 0xFF));
        s.WriteByte((byte)((val >> 24) & 0xFF));
    }

    private class Message
    {
        public uint Salt;
        public uint Ts;
        public readonly Dictionary<ushort, uint> Privileges = new();

        public byte[] Pack()
        {
            using var ms = new MemoryStream();
            WriteUint32(ms, Salt);
            WriteUint32(ms, Ts);
            WriteUint16(ms, (ushort)Privileges.Count);
            foreach (var kv in Privileges.OrderBy(x => x.Key))
            {
                WriteUint16(ms, kv.Key);
                WriteUint32(ms, kv.Value);
            }
            return ms.ToArray();
        }
    }
}
