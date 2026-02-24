using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Common.Models;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Video.Commands.GenerateVideoToken;

public record GenerateVideoTokenCommand(
    string RoomName,
    bool IsHost = false) : IRequest<Result<VideoTokenDto>>;

public record VideoTokenDto(string Token, string RoomName, int ExpiresInSeconds);

public class GenerateVideoTokenCommandHandler
    : IRequestHandler<GenerateVideoTokenCommand, Result<VideoTokenDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IVideoService _videoService;
    private readonly IProcessHistoryService _history;
    private readonly IPlatformSettingService _settings;

    public GenerateVideoTokenCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IVideoService videoService,
        IProcessHistoryService history,
        IPlatformSettingService settings)
    {
        _context = context;
        _currentUser = currentUser;
        _videoService = videoService;
        _history = history;
        _settings = settings;
    }

    public async Task<Result<VideoTokenDto>> Handle(
        GenerateVideoTokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return Result<VideoTokenDto>.Failure("User not authenticated");

        var userId = _currentUser.UserId.Value;

        // Helper: find ACTIVE (non-Ended) session by RoomName, en son oluşturulanı döndür
        async Task<VideoSession?> findSessionByRoomName(string roomName)
        {
            var s = await _context.VideoSessions
                .Where(vs => vs.RoomName == roomName && vs.Status != Domain.Enums.VideoSessionStatus.Ended)
                .OrderByDescending(vs => vs.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (s != null) return s;

            // Fallback: try to find by ResourceId from room name (e.g., group-class-{guid})
            if (roomName.StartsWith("group-class-"))
            {
                var idPart = roomName.Replace("group-class-", "");
                if (Guid.TryParse(idPart, out var classId))
                {
                    s = await _context.VideoSessions
                        .Where(vs => vs.ResourceId == classId &&
                                     vs.Status != Domain.Enums.VideoSessionStatus.Ended)
                        .OrderByDescending(vs => vs.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }
            }
            return s;
        }

        // Helper: eski (stale) session'ları temizle — tüm eski Live/Scheduled session'ları Ended yap
        // Twilio'da stale room varsa da kapatır (yenisi mentor bağlanırken otomatik oluşturulur)
        async Task cleanupStaleSessions(string roomName)
        {
            var activeSessions = await _context.VideoSessions
                .Where(vs => vs.RoomName == roomName && vs.Status != Domain.Enums.VideoSessionStatus.Ended)
                .ToListAsync(cancellationToken);

            foreach (var stale in activeSessions)
            {
                stale.MarkAsEnded();
            }

            if (activeSessions.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Twilio'da da eski room varsa kapat (isHost=true olduğunda yenisi otomatik oluşturulacak)
            try { await _videoService.CompleteRoomAsync(roomName, cancellationToken); } catch { }
        }

        // ──── Sunucu tarafında isHost doğrulaması ────
        // Client'ın gönderdiği isHost değerine güvenmiyoruz, sunucu tarafında hesaplıyoruz
        bool serverIsHost = false;

        // Booking status kontrolü — iptal edilen/tamamlanan seanslara token verilmez
        if (Guid.TryParse(request.RoomName, out var parsedBookingId))
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == parsedBookingId, cancellationToken);
            if (booking != null && booking.Status != Domain.Enums.BookingStatus.Confirmed)
                return Result<VideoTokenDto>.Failure(
                    $"Bu seans için video token oluşturulamaz. Seans durumu: {booking.Status}");

            // Sunucu tarafında host belirleme — mentor = host
            if (booking != null)
                serverIsHost = booking.MentorUserId == userId;

            // Erken katılım kontrolü — DevMode kapalıysa en erken 15dk önce başlatılabilir
            if (booking != null)
            {
                var devMode = await _settings.GetBoolAsync(PlatformSettings.DevModeSessionBypass, false, cancellationToken);
                if (!devMode)
                {
                    var earlyMinutes = await _settings.GetIntAsync(PlatformSettings.SessionEarlyJoinMinutes, 15, cancellationToken);
                    var minutesUntilStart = (booking.StartAt - DateTime.UtcNow).TotalMinutes;
                    if (minutesUntilStart > earlyMinutes)
                        return Result<VideoTokenDto>.Failure(
                            $"Ders en erken {earlyMinutes} dakika önce başlatılabilir. Ders başlangıcına {Math.Ceiling(minutesUntilStart)} dakika kaldı.");
                }

                // ──── Öğrenci için mentor aktivasyon kontrolü ────
                // Öğrenci (host değil) token istiyorsa, mentor'ün odayı aktifleştirmiş olması gerekir
                if (!serverIsHost)
                {
                    var session = await findSessionByRoomName(request.RoomName);
                    if (session == null || session.Status != Domain.Enums.VideoSessionStatus.Live)
                    {
                        return Result<VideoTokenDto>.Failure(
                            "Mentor henüz odayı aktifleştirmedi. Lütfen bekleyin.");
                    }
                }
            }
        }

        // Group class early join kontrolü
        if (request.RoomName.StartsWith("group-class-"))
        {
            var gcIdPart = request.RoomName.Replace("group-class-", "");
            if (Guid.TryParse(gcIdPart, out var gcId))
            {
                var groupClass = await _context.GroupClasses
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.Id == gcId, cancellationToken);

                if (groupClass == null)
                    return Result<VideoTokenDto>.Failure("Grup dersi bulunamadı");

                if (groupClass.Status != Domain.Enums.ClassStatus.Published)
                    return Result<VideoTokenDto>.Failure(
                        $"Bu grup dersi için video token oluşturulamaz. Ders durumu: {groupClass.Status}");

                // Verify user is mentor or has confirmed enrollment
                var isMentor = groupClass.MentorUserId == userId;
                var isEnrolled = groupClass.Enrollments.Any(e =>
                    e.StudentUserId == userId && e.Status == Domain.Enums.EnrollmentStatus.Confirmed);

                if (!isMentor && !isEnrolled)
                    return Result<VideoTokenDto>.Failure("Bu derse katılma yetkiniz yok");

                // Sunucu tarafında host belirleme — mentor = host
                serverIsHost = isMentor;

                // Early join check — DevMode bypass
                var gcDevMode = await _settings.GetBoolAsync(PlatformSettings.DevModeSessionBypass, false, cancellationToken);
                if (!gcDevMode)
                {
                    var gcEarlyMinutes = await _settings.GetIntAsync(PlatformSettings.SessionEarlyJoinMinutes, 15, cancellationToken);
                    var gcMinutesUntilStart = (groupClass.StartAt - DateTime.UtcNow).TotalMinutes;
                    if (gcMinutesUntilStart > gcEarlyMinutes)
                        return Result<VideoTokenDto>.Failure(
                            $"Ders en erken {gcEarlyMinutes} dakika önce başlatılabilir. Ders başlangıcına {Math.Ceiling(gcMinutesUntilStart)} dakika kaldı.");
                }

                // ──── Öğrenci için mentor aktivasyon kontrolü (grup dersleri) ────
                if (!serverIsHost)
                {
                    var gcSession = await findSessionByRoomName(request.RoomName);
                    if (gcSession == null || gcSession.Status != Domain.Enums.VideoSessionStatus.Live)
                    {
                        return Result<VideoTokenDto>.Failure(
                            "Mentor henüz odayı aktifleştirmedi. Lütfen bekleyin.");
                    }
                }
            }
        }

        // Get user info
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
            return Result<VideoTokenDto>.Failure("User not found");

        // Generate token — sunucu tarafında hesaplanan isHost kullan
        var tokenResult = await _videoService.GenerateTokenAsync(
            request.RoomName,
            userId,
            user.DisplayName,
            serverIsHost,
            cancellationToken);

        if (!tokenResult.Success)
            return Result<VideoTokenDto>.Failure(tokenResult.ErrorMessage ?? "Failed to generate token");

        // Eğer host (mentor) ise, eski session'ları temizle ve yeni session oluştur/Live yap
        if (serverIsHost)
        {
            // ──── Önce eski stale session'ları temizle ────
            await cleanupStaleSessions(request.RoomName);

            // Şimdi yeni session oluştur veya mevcut Scheduled olanı Live yap
            var session = await findSessionByRoomName(request.RoomName);

            if (session != null)
            {
                session.MarkAsLive();
                await _context.SaveChangesAsync(cancellationToken);

                await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                    "Scheduled", "Live",
                    $"Mentor odaya katıldı, session başlatıldı. Room: {request.RoomName}",
                    userId, "Mentor", ct: cancellationToken);
            }
            else if (Guid.TryParse(request.RoomName, out var bookingId))
            {
                // Yeni session oluştur (eski stale'ler temizlendi)
                session = VideoSession.Create("Booking", bookingId, request.RoomName);
                _context.VideoSessions.Add(session);
                session.MarkAsLive();
                await _context.SaveChangesAsync(cancellationToken);

                await _history.LogAsync("VideoSession", session.Id, "StatusChanged",
                    "Scheduled", "Live",
                    $"Mentor odaya katıldı, yeni session başlatıldı. Room: {request.RoomName}",
                    userId, "Mentor", ct: cancellationToken);
            }
        }

        // Track participant
        var existingSession = await findSessionByRoomName(request.RoomName);

        if (existingSession != null)
        {
            // Close any open (active) segment for this user in this session before creating a new one.
            // This prevents duplicate open segments when user refreshes the page or reconnects.
            var openSegment = await _context.VideoParticipants
                .FirstOrDefaultAsync(p =>
                    p.VideoSessionId == existingSession.Id &&
                    p.UserId == userId &&
                    !p.LeftAt.HasValue,
                    cancellationToken);

            if (openSegment != null)
            {
                openSegment.Leave();
            }

            var participant = VideoParticipant.Create(existingSession.Id, userId);
            _context.VideoParticipants.Add(participant);
            await _context.SaveChangesAsync(cancellationToken);

            var role = serverIsHost ? "Mentor" : "Student";
            await _history.LogAsync("VideoSession", existingSession.Id, "ParticipantJoined",
                null, null,
                $"{role} ({user.DisplayName}) odaya katıldı. Room: {request.RoomName}",
                userId, role, ct: cancellationToken);
        }

        return Result<VideoTokenDto>.Success(
            new VideoTokenDto(tokenResult.Token!, request.RoomName, 14400));
    }
}