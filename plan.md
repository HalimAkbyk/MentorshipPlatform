# Mentor Ödeme Talebi & Admin Ödeme Yapma Sistemi

## Mevcut Durum
- **Ledger sistemi** var: LedgerEntry entity, MentorEscrow → MentorAvailable → MentorPayout akışı.
- **Mentor earnings sayfası** var: bakiye kartları + işlem geçmişi (sadece okuma).
- **Admin payouts sayfası** var: mentor listesi + detay drawer (sadece okuma).
- **PlatformSetting** altyapısı var: key-value, cache, admin CRUD.
- **Eksik olan**: Ödeme talep etme, ödeme yapma, min tutar parametresi.

---

## Plan

### ADIM 1 — Domain: PayoutRequest Entity
`Domain/Entities/PayoutRequest.cs`

```
PayoutRequest : BaseEntity
  - MentorUserId (Guid)
  - Amount (decimal)
  - Currency (string, default "TRY")
  - Status (PayoutRequestStatus enum: Pending, Approved, Rejected, Completed)
  - MentorNote (string?, mentor'un notu)
  - AdminNote (string?, admin'in notu)
  - ProcessedByUserId (Guid?, işlemi yapan admin)
  - ProcessedAt (DateTime?)
  - Create() factory method
  - Approve(), Reject(), Complete() methods
```

`Domain/Enums/PayoutRequestStatus.cs`: Pending, Approved, Rejected, Completed

### ADIM 2 — Persistence: EF Config + Migration
- `Persistence/Configurations/PayoutRequestConfiguration.cs`
- `IApplicationDbContext`'e `DbSet<PayoutRequest> PayoutRequests` ekle
- Migration oluştur

### ADIM 3 — PlatformSetting: Minimum Ödeme Tutarı
- `IPlatformSettingService.cs`'deki `PlatformSettings` sınıfına:
  ```csharp
  public const string MinimumPayoutAmount = "minimum_payout_amount";
  ```
- `AdminSettingsController.SeedDefaultSettings()`'e default ekle:
  ```
  ("minimum_payout_amount", "100", "Mentorların talep edebileceği minimum ödeme tutarı (TRY)", "Limits")
  ```

### ADIM 4 — Application: Mentor Komutları
**A) `Application/Payouts/Commands/CreatePayoutRequest/`**
- Mentor kendi availableBalance'ından ödeme talep eder
- Validasyon: amount > 0, amount ≤ availableBalance, amount ≥ minimumPayoutAmount
- Aktif (Pending) talep varsa yeni talep oluşturulamaz
- PayoutRequest oluştur (status: Pending)

**B) `Application/Payouts/Queries/GetMyPayoutRequests/`**
- Mentor'un kendi ödeme talepleri (paginated)

**C) `Application/Payouts/Queries/GetPayoutSettings/`**
- Public query: minimum ödeme tutarını ve mentor'un mevcut bakiyesini döner (frontend bilgi mesajları için)

### ADIM 5 — Application: Admin Komutları
**A) `Application/Payouts/Commands/ProcessPayoutRequest/`**
- Admin bir talebi Approve+Complete (ödeme yap) veya Reject edebilir
- Approve+Complete: MentorAvailable'dan Debit + MentorPayout'a Credit ledger entry'leri oluştur
- Reject: sadece status güncelle + adminNote

**B) `Application/Payouts/Queries/GetAllPayoutRequests/`**
- Admin tüm ödeme taleplerini görebilir (filtreleme: status, mentor adı, tarih)

### ADIM 6 — API: Controller Endpoints
**`Api/Controllers/PayoutRequestsController.cs`**

Mentor (RequireMentorRole):
- `POST /api/payout-requests` → CreatePayoutRequest
- `GET /api/payout-requests` → GetMyPayoutRequests
- `GET /api/payout-requests/settings` → GetPayoutSettings (min tutar + bakiye)

Admin (RequireAdminRole):
- `GET /api/admin/payout-requests` → GetAllPayoutRequests
- `PUT /api/admin/payout-requests/{id}/process` → ProcessPayoutRequest (approve/reject/complete)

### ADIM 7 — Frontend: API + Hooks
- `lib/api/payouts.ts`: mentor ve admin API fonksiyonları
- `lib/hooks/use-payouts.ts`: React Query hooks

### ADIM 8 — Frontend: Mentor Kazançlarım Sayfasını Güncelle
`/mentor/earnings/page.tsx`'e eklenecekler:
- **"Ödeme Talep Et" butonu** (bakiye kartları yanında)
- Butona tıklanınca **modal/dialog**: tutar girişi + info mesajı (min tutar bilgisi)
- Minimum tutar altındaysa buton disabled + uyarı mesajı göster
- **Ödeme Taleplerim bölümü**: mevcut ve geçmiş talepler, durum badge'leri (Bekliyor/Onaylandı/Reddedildi/Tamamlandı)

### ADIM 9 — Frontend: Admin Ödemeler Sayfasını Güncelle
`/admin/payouts/page.tsx`'e eklenecekler:
- **Ödeme Talepleri sekmesi/bölümü**: tüm mentor talepleri listesi
- Status badge'leri (Bekliyor → sarı, Onaylandı → mavi, Tamamlandı → yeşil, Reddedildi → kırmızı)
- Her talep satırında: mentor adı, tutar, tarih, durum, işlem butonları
- **"Onayla & Öde" butonu**: talebi onayla + ledger kaydı oluştur
- **"Reddet" butonu**: red nedeni girebileceği dialog
- Detail drawer'da talep geçmişi

### ADIM 10 — Frontend: Admin Ayarlar/Ücretler Sayfasını Güncelle
`/admin/settings/fees/page.tsx`'e `minimum_payout_amount` ayarını ekle:
- Mevcut fee ayarları yanında "Minimum Ödeme Tutarı" input'u
- Bilgi açıklaması: "Bu tutarın altında mentor ödeme talebinde bulunamaz"

### ADIM 11 — Build, Commit, Deploy
- Backend build + Docker v84 + Koyeb deploy
- Frontend build + push (Vercel auto-deploy)
