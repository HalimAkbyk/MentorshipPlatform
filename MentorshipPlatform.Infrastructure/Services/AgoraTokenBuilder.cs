namespace MentorshipPlatform.Infrastructure.Services;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Agora AccessToken2 (007) builder — required for newer Agora projects.
/// Based on the official algorithm from github.com/AgoraIO/Tools.
/// </summary>
public static class AgoraTokenBuilder
{
    private const string Version = "007";

    // Service types
    private const ushort ServiceTypeRtc = 1;

    // RTC privileges
    private const ushort PrivilegeJoinChannel = 1;
    private const ushort PrivilegePublishAudioStream = 2;
    private const ushort PrivilegePublishVideoStream = 3;
    private const ushort PrivilegePublishDataStream = 4;

    /// <summary>
    /// Build an RTC AccessToken2 (007 format).
    /// uid = "0" for wildcard, or a specific numeric uid as string.
    /// </summary>
    public static string BuildToken(
        string appId,
        string appCertificate,
        string channelName,
        string uid,
        uint tokenExpireSeconds = 3600)
    {
        var issueTs = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var salt = (uint)Random.Shared.Next(1, 99999999);

        var expire = tokenExpireSeconds; // token-level expire (seconds from issue)
        var privilegeExpire = tokenExpireSeconds; // privilege-level expire

        // Generate signature
        var signature = GetSign(appCertificate, issueTs, salt);

        // Build content
        using var content = new MemoryStream();

        // 1. Signature
        PackString(content, signature);

        // 2. Issue timestamp
        PackUint32(content, issueTs);

        // 3. Expire (seconds)
        PackUint32(content, expire);

        // 4. Services count
        PackUint16(content, 1); // only RTC service

        // 5. RTC Service
        PackUint16(content, ServiceTypeRtc);

        // Service-specific fields: channelName + uid
        PackString(content, Encoding.UTF8.GetBytes(channelName));
        PackString(content, Encoding.UTF8.GetBytes(uid));

        // Privileges
        PackUint16(content, 4); // 4 privileges
        PackUint16(content, PrivilegeJoinChannel);
        PackUint32(content, privilegeExpire);
        PackUint16(content, PrivilegePublishAudioStream);
        PackUint32(content, privilegeExpire);
        PackUint16(content, PrivilegePublishVideoStream);
        PackUint32(content, privilegeExpire);
        PackUint16(content, PrivilegePublishDataStream);
        PackUint32(content, privilegeExpire);

        // Compress with zlib
        var compressed = CompressZlib(content.ToArray());

        // Final: "007" + appId (plain) + base64(compressed)
        return Version + appId + Convert.ToBase64String(compressed);
    }

    private static byte[] GetSign(string appCertificate, uint issueTs, uint salt)
    {
        // Round 1: HMAC(key=issueTs_LE_bytes, data=appCertificate)
        var issueTsBytes = new byte[4];
        issueTsBytes[0] = (byte)(issueTs & 0xFF);
        issueTsBytes[1] = (byte)((issueTs >> 8) & 0xFF);
        issueTsBytes[2] = (byte)((issueTs >> 16) & 0xFF);
        issueTsBytes[3] = (byte)((issueTs >> 24) & 0xFF);

        using var hmac1 = new HMACSHA256(issueTsBytes);
        var signKey = hmac1.ComputeHash(Encoding.UTF8.GetBytes(appCertificate));

        // Round 2: HMAC(key=salt_LE_bytes, data=signKey)
        var saltBytes = new byte[4];
        saltBytes[0] = (byte)(salt & 0xFF);
        saltBytes[1] = (byte)((salt >> 8) & 0xFF);
        saltBytes[2] = (byte)((salt >> 16) & 0xFF);
        saltBytes[3] = (byte)((salt >> 24) & 0xFF);

        using var hmac2 = new HMACSHA256(saltBytes);
        return hmac2.ComputeHash(signKey);
    }

    private static void PackString(Stream s, byte[] val)
    {
        PackUint16(s, (ushort)val.Length);
        s.Write(val, 0, val.Length);
    }

    private static void PackUint16(Stream s, ushort val)
    {
        s.WriteByte((byte)(val & 0xFF));
        s.WriteByte((byte)((val >> 8) & 0xFF));
    }

    private static void PackUint32(Stream s, uint val)
    {
        s.WriteByte((byte)(val & 0xFF));
        s.WriteByte((byte)((val >> 8) & 0xFF));
        s.WriteByte((byte)((val >> 16) & 0xFF));
        s.WriteByte((byte)((val >> 24) & 0xFF));
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var output = new MemoryStream();
        // Zlib = 2-byte header + deflate + 4-byte checksum
        // Write zlib header (default compression)
        output.WriteByte(0x78);
        output.WriteByte(0x9C);

        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        // Write Adler-32 checksum
        var adler = ComputeAdler32(data);
        output.WriteByte((byte)((adler >> 24) & 0xFF));
        output.WriteByte((byte)((adler >> 16) & 0xFF));
        output.WriteByte((byte)((adler >> 8) & 0xFF));
        output.WriteByte((byte)(adler & 0xFF));

        return output.ToArray();
    }

    private static uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}
