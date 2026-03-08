namespace MentorshipPlatform.Infrastructure.Services;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Agora AccessToken2 (007) builder.
/// Ported exactly from the official C# implementation at:
/// https://github.com/AgoraIO/Tools/tree/master/DynamicKey/AgoraDynamicKey/csharp
/// </summary>
public static class AgoraTokenBuilder
{
    private const string Version = "007";
    private const ushort ServiceTypeRtc = 1;

    private const ushort PrivilegeJoinChannel = 1;
    private const ushort PrivilegePublishAudioStream = 2;
    private const ushort PrivilegePublishVideoStream = 3;
    private const ushort PrivilegePublishDataStream = 4;

    /// <summary>
    /// Build an RTC AccessToken2.
    /// uid: "0" or "" for wildcard, or numeric uid as string.
    /// tokenExpireSeconds: token lifetime in seconds (e.g. 3600).
    /// privilegeExpireSeconds: privilege lifetime in seconds (e.g. 3600).
    /// </summary>
    public static string BuildToken(
        string appId,
        string appCertificate,
        string channelName,
        string uid,
        uint tokenExpireSeconds = 3600,
        uint privilegeExpireSeconds = 3600)
    {
        var issueTs = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var salt = (uint)Random.Shared.Next(1, 99999999);

        // ── Step 1: Build the buffer (appId + issueTs + expire + salt + services) ──
        var buf = new BufWriter();
        buf.PutBytes(Encoding.UTF8.GetBytes(appId));      // put = uint16 length + data
        buf.PutUint32(issueTs);
        buf.PutUint32(tokenExpireSeconds);
        buf.PutUint32(salt);
        buf.PutUint16(1); // services count = 1 (RTC only)

        // Service: serviceType + privileges + channelName + uid
        // (base.pack adds type + privileges, then ServiceRtc adds channelName + uid)
        buf.PutUint16(ServiceTypeRtc);

        // Privileges map
        buf.PutUint16(4); // 4 privileges
        buf.PutUint16(PrivilegeJoinChannel);
        buf.PutUint32(privilegeExpireSeconds);
        buf.PutUint16(PrivilegePublishAudioStream);
        buf.PutUint32(privilegeExpireSeconds);
        buf.PutUint16(PrivilegePublishVideoStream);
        buf.PutUint32(privilegeExpireSeconds);
        buf.PutUint16(PrivilegePublishDataStream);
        buf.PutUint32(privilegeExpireSeconds);

        // ServiceRtc-specific: channelName + uid
        buf.PutBytes(Encoding.UTF8.GetBytes(channelName));
        buf.PutBytes(Encoding.UTF8.GetBytes(uid));

        var bufBytes = buf.ToArray();

        // ── Step 2: Generate signing key (2 HMAC rounds) ──
        var signing = GetSign(appCertificate, issueTs, salt);

        // ── Step 3: Sign the buffer content (3rd HMAC round) ──
        var signature = HmacSha256(signing, bufBytes);

        // ── Step 4: Build final content = put(signature) + copy(buf) ──
        var content = new BufWriter();
        content.PutBytes(signature);   // put = with uint16 length prefix
        content.CopyRaw(bufBytes);     // copy = raw bytes, no length prefix

        // ── Step 5: Compress + encode ──
        var compressed = CompressZlib(content.ToArray());
        return Version + Convert.ToBase64String(compressed);
    }

    /// <summary>
    /// Two-round HMAC signing:
    /// Round 1: HMAC(key=issueTs_LE, data=appCert)
    /// Round 2: HMAC(key=salt_LE, data=round1_result)
    /// </summary>
    private static byte[] GetSign(string appCertificate, uint issueTs, uint salt)
    {
        var issueTsBytes = BitConverter.GetBytes(issueTs); // LE on little-endian systems
        var saltBytes = BitConverter.GetBytes(salt);

        var signKey = HmacSha256(issueTsBytes, Encoding.UTF8.GetBytes(appCertificate));
        return HmacSha256(saltBytes, signKey);
    }

    private static byte[] HmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Simple buffer writer matching Agora's ByteBuf behavior.
    /// </summary>
    private class BufWriter
    {
        private readonly MemoryStream _ms = new();

        /// <summary>Put bytes with uint16 length prefix (like ByteBuf.put(byte[]))</summary>
        public void PutBytes(byte[] val)
        {
            PutUint16((ushort)val.Length);
            _ms.Write(val, 0, val.Length);
        }

        /// <summary>Copy raw bytes without length prefix (like ByteBuf.copy(byte[]))</summary>
        public void CopyRaw(byte[] val)
        {
            _ms.Write(val, 0, val.Length);
        }

        public void PutUint16(ushort val)
        {
            _ms.WriteByte((byte)(val & 0xFF));
            _ms.WriteByte((byte)((val >> 8) & 0xFF));
        }

        public void PutUint32(uint val)
        {
            _ms.WriteByte((byte)(val & 0xFF));
            _ms.WriteByte((byte)((val >> 8) & 0xFF));
            _ms.WriteByte((byte)((val >> 16) & 0xFF));
            _ms.WriteByte((byte)((val >> 24) & 0xFF));
        }

        public byte[] ToArray() => _ms.ToArray();
    }
}
