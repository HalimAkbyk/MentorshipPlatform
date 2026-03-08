namespace MentorshipPlatform.Infrastructure.Services;

public class AgoraOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppCertificate { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerSecret { get; set; } = string.Empty;
    public AgoraWhiteboardOptions Whiteboard { get; set; } = new();
}

public class AgoraWhiteboardOptions
{
    public string AppIdentifier { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "eu";
}
