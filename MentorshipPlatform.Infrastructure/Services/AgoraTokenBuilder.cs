namespace MentorshipPlatform.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;

public static class AgoraTokenBuilder
{
    // Privilege constants
    private const ushort KJoinChannel = 1;
    private const ushort KPublishAudioStream = 2;
    private const ushort KPublishVideoStream = 3;
    private const ushort KPublishDataStream = 4;

    public static string BuildToken(
        string appId,
        string appCertificate,
        string channelName,
        string account,
        uint privilegeExpiredTs)
    {
        // Use RtcTokenBuilder approach
        var msg = new Message();
        msg.Salt = (uint)new Random().Next(1, 99999999);
        msg.Ts = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        msg.Privileges[KJoinChannel] = privilegeExpiredTs;
        msg.Privileges[KPublishAudioStream] = privilegeExpiredTs;
        msg.Privileges[KPublishVideoStream] = privilegeExpiredTs;
        msg.Privileges[KPublishDataStream] = privilegeExpiredTs;

        var msgBytes = msg.Pack();

        // Sign
        var toSign = Encoding.UTF8.GetBytes(appId)
            .Concat(Encoding.UTF8.GetBytes(channelName))
            .Concat(Encoding.UTF8.GetBytes(account))
            .Concat(msgBytes)
            .ToArray();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appCertificate));
        var signature = hmac.ComputeHash(toSign);

        // Build final token
        var content = PackString(appId)
            .Concat(PackBytes(signature))
            .Concat(PackBytes(msgBytes))
            .ToArray();

        return "006" + Convert.ToBase64String(content);
    }

    private static byte[] PackString(string val)
    {
        var bytes = Encoding.UTF8.GetBytes(val);
        return PackUint16((ushort)bytes.Length).Concat(bytes).ToArray();
    }

    private static byte[] PackBytes(byte[] val)
    {
        return PackUint16((ushort)val.Length).Concat(val).ToArray();
    }

    private static byte[] PackUint16(ushort val)
    {
        return new[] { (byte)(val & 0xFF), (byte)((val >> 8) & 0xFF) };
    }

    private static byte[] PackUint32(uint val)
    {
        return new[] {
            (byte)(val & 0xFF),
            (byte)((val >> 8) & 0xFF),
            (byte)((val >> 16) & 0xFF),
            (byte)((val >> 24) & 0xFF)
        };
    }

    private class Message
    {
        public uint Salt;
        public uint Ts;
        public Dictionary<ushort, uint> Privileges = new();

        public byte[] Pack()
        {
            var result = new List<byte>();
            result.AddRange(PackUint32(Salt));
            result.AddRange(PackUint32(Ts));
            result.AddRange(PackUint16((ushort)Privileges.Count));
            foreach (var kv in Privileges.OrderBy(x => x.Key))
            {
                result.AddRange(PackUint16(kv.Key));
                result.AddRange(PackUint32(kv.Value));
            }
            return result.ToArray();
        }
    }
}
