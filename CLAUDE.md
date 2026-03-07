# CLAUDE.md

## Project Overview

MentorshipPlatform is a mentorship and skill-sharing marketplace built with ASP.NET Core 8.0. It connects mentors with students for booking sessions, video conferencing, payments, and class management.

## Tech Stack

- **Language:** C# / .NET 8.0
- **Framework:** ASP.NET Core 8.0
- **Database:** PostgreSQL (via EF Core 8.0 + Npgsql)
- **Cache:** Redis (StackExchange.Redis)
- **Storage:** MinIO (S3-compatible object storage)
- **Video:** Twilio
- **Payments:** Iyzipay
- **Background Jobs:** Hangfire
- **Logging:** Serilog

## Architecture

Clean Architecture with CQRS (MediatR) and FluentValidation.

```
MentorshipPlatform.Api/            → Controllers, middleware, DI config
MentorshipPlatform.Application/    → Commands, queries, validators, interfaces
MentorshipPlatform.Domain/         → Entities, enums, domain events
MentorshipPlatform.Persistence/    → EF Core DbContext, configurations, migrations
MentorshipPlatform.Infrastructure/ → External service implementations
MentorshipPlatform.Identity/       → JWT token generation
```

**Dependency flow:** Api → Application → Domain. Persistence and Infrastructure implement Application interfaces.

## Build & Run

```bash
# Build
dotnet build MentorshipPlatform.sln

# Run (dev)
dotnet run --project MentorshipPlatform.Api

# Docker
docker-compose -f compose.yaml up
```

- HTTP: http://localhost:5072
- HTTPS: https://localhost:7046
- Swagger: http://localhost:5072/swagger
- Frontend expects: http://localhost:3000

## Database

```bash
# Apply migrations
dotnet ef database update --project MentorshipPlatform.Persistence --startup-project MentorshipPlatform.Api
```

Migrations auto-apply in development mode via `Program.cs`.

## Key Patterns

- **CQRS:** Commands and queries in `Application/{Feature}/Commands/` and `Queries/`
- **Validation:** FluentValidation validators co-located with command/query handlers
- **Pipeline behaviors:** `ValidationBehaviour.cs` for MediatR pipeline
- **Domain events:** Dispatched via `BaseEntity` in Domain layer
- **Authorization:** Role-based policies (Student, Mentor, Admin) with JWT Bearer
- **Result pattern:** `Application/Common/Models/Result.cs` for operation results

## Configuration

Settings in `appsettings.json` / `appsettings.Development.json`:
- `ConnectionStrings:DefaultConnection` — PostgreSQL (port 5433)
- `ConnectionStrings:Redis` — Redis (port 6379)
- `Jwt` — Secret, issuer, audience, token expiration
- `Iyzico`, `Twilio`, `Minio`, `Email` — External service credentials

## Code Conventions

- Feature-based folder organization under Application layer
- One command/query per folder with handler and validator
- Interfaces defined in `Application/Common/Interfaces/`
- Entity configurations in `Persistence/Configurations/`
- No test projects currently exist

## Pivot: Dikey Eğitim Modeli (v1.2)

Platform marketplace modelinden TYT/AYT odaklı dikey eğitim modeline geçiyor.
Detaylı analiz: `/Users/halimakbiyik/Desktop/MentorApp/degisim-mentorluk-pivot-analizi-v1.2.md`

### Özet Değişiklikler
- Mentor alımı dışarıya kapalı (admin ataması ile), feature flag ile geri açılabilir
- Paket & Kredi sistemi (yeni): Öğrenci paket satın alır, kredi olarak kullanır
- Eğitmen Performans & Hakediş modülü (yeni): Session tracking, video izleme, raporlama
- Mevcut modüller flag ile pasife çekilebilir (MARKETPLACE_MODE, EXTERNAL_MENTOR_REGISTRATION, vb.)
- UI'da "Mentor" → "Eğitmen" terminolojisi (kod tarafında UserRole.Mentor korunur)

### Geliştirme Sırası (Faz)
1. **Faz 1**: Yeni feature flag'ler + DB migration + mevcut modülleri flag ile sarmalama
2. **Faz 2**: Eğitmen atama (admin) + yetki uyarlama
3. **Faz 3**: İçerik yönetimi + eğitmen etiketleme + TYT/AYT kategoriler
4. **Faz 4**: Paket & Kredi sistemi (en kapsamlı yeni modül)
5. **Faz 5**: Eğitmen Performans & Hakediş
6. **Faz 6**: Test & Lansman

### Son Tamamlanan İşler
- SignalR ile anlık oda durumu (polling kaldırıldı)
- Menü yapısı yenilendi (Genel, İçerik Yönetimi, Kazanç, Katılımlarım, Hesap)
- Backend v90 deploy edildi (Koyeb), frontend Vercel'de
