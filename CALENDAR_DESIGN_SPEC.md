# Takvim Sistemi – Uçtan Uca Tasarım Spesifikasyonu

> **Platform:** Degişim Mentorluk
> **Tarih:** 2025-02-13
> **Stack:** Next.js 14 + FullCalendar (OSS) + .NET 8 Backend
> **Referans Ürünler:** Calendly, Cal.com, MentorCruise, Superpeer

---

## İÇİNDEKİLER

1. [Information Architecture (IA)](#1-information-architecture)
2. [FullCalendar View Tasarımı](#2-fullcalendar-view-tasarımı)
3. [Event / Slot Veri Modeli](#3-event--slot-veri-modeli)
4. [Status Makineleri](#4-status-makineleri)
5. [UI Kuralları](#5-ui-kuralları)
6. [Düzenlenemez (Locked) Senaryoları](#6-düzenlenemez-locked-senaryoları)
7. [Status-Bazlı Kilit Matrisi](#7-status-bazlı-kilit-matrisi)
8. [Concurrency & Kritik Edge-Case Senaryoları](#8-concurrency--kritik-edge-case-senaryoları)
9. [Lock Modeli Önerisi](#9-lock-modeli-önerisi)
10. [FullCalendar Entegrasyon Stratejisi](#10-fullcalendar-entegrasyon-stratejisi)
11. [Wireframe Açıklamaları](#11-wireframe-açıklamaları)

---

## 1. INFORMATION ARCHITECTURE

### 1.1 Mentor Paneli Ekranları

| # | Ekran | Amaç | Ana Bileşenler |
|---|-------|-------|-----------------|
| 1 | **Availability Yönetimi** `/mentor/availability` | Haftalık müsaitlik şablonu + tarih bazlı istisnalar tanımlama | FullCalendar `timeGridWeek` + sağ drawer (haftalık şablon formu) + override takvimi |
| 2 | **Randevu Takvimi** `/mentor/bookings` | Onaylanmış, bekleyen, tamamlanmış tüm seansları görüntüleme | FullCalendar `timeGridWeek` / `dayGridMonth` + filtre toolbar + sağ drawer (booking detay) |
| 3 | **Seans Detay & Classroom** `/mentor/bookings/[id]` | Tekil booking yönetimi: iptal, tamamla, video başlat, notlar | Booking summary card + video launcher + timeline/history + action buttons |
| 4 | **Dashboard** `/mentor/dashboard` | Günün programı, yaklaşan seanslar, kazanç özeti, hızlı aksiyonlar | Today's schedule widget + upcoming bookings list + earnings summary + quick actions |

### 1.2 Öğrenci Paneli Ekranları

| # | Ekran | Amaç | Ana Bileşenler |
|---|-------|-------|-----------------|
| 1 | **Mentor Keşfet & Slot Seçimi** `/public/mentors/[id]` | Mentor profilinde müsait slotları görüp seçme | Mentor info card + FullCalendar `dayGridMonth` (sol) + slot listesi (sağ) + fiyat/süre bilgisi |
| 2 | **Rezervasyonlarım** `/student/bookings` | Aktif, geçmiş ve iptal edilen tüm bookingleri listeleme | FullCalendar `listWeek` / `dayGridMonth` + filtre tabs (Aktif / Geçmiş / İptal) + sağ drawer |
| 3 | **Booking Detay** `/student/bookings/[id]` | Tekil booking: ödeme durumu, video erişimi, iptal/dispute | Booking summary + payment status + video launcher + cancel/dispute buttons + timeline |
| 4 | **Ödeme Akışı** `/student/bookings/new` | Slot seçimi → Bilgi girişi → Ödeme → Onay | Multi-step wizard: slot confirm → buyer info → iyzico checkout → success/fail |

### 1.3 Admin Paneli Ekranları

| # | Ekran | Amaç | Ana Bileşenler |
|---|-------|-------|-----------------|
| 1 | **Master Takvim** `/admin/calendar` | Tüm mentor/öğrenci bookinglerinin tek takvimde görüntülenmesi | FullCalendar `timeGridWeek` + sol filtre paneli (mentor, öğrenci, statü, tarih) + sağ drawer |
| 2 | **Anomali Panosu** `/admin/anomalies` | Ödeme-booking uyumsuzlukları, no-show, dispute listesi | Anomaly cards (ödeme var/booking yok, booking var/ödeme yok, video join mismatch) + aksiyon butonları |
| 3 | **Dispute Yönetimi** `/admin/disputes` | Öğrenci şikayetlerini inceleme ve çözümleme | Dispute list + detail drawer (booking info + payment info + video logs + chat) + resolve actions |
| 4 | **Audit Log** `/admin/audit` | Tüm sistem değişikliklerinin kronolojik kaydı | Filtrelenebilir log tablosu: entity, action, user, timestamp, before/after JSON |

---

## 2. FULLCALENDAR VIEW TASARIMI

### 2.1 Mentor: Availability Edit

**Calendly İlham:** Haftalık tekrarlayan saatler + tarih bazlı override'lar.

#### Haftalık Şablon (Weekly Template)
```
┌─────────────────────────────────────────────────────────────────┐
│ Haftalık Müsaitlik Şablonu                                      │
├──────────┬────────────────────────────────────────┬─────────────┤
│ Pazartesi │ [09:00] - [12:00]  [+]                │ ☑ Aktif     │
│           │ [14:00] - [18:00]  [🗑]               │             │
│ Salı      │ [09:00] - [17:00]  [+]                │ ☑ Aktif     │
│ Çarşamba  │ [Tüm gün kapalı]                      │ ☐ Kapalı    │
│ Perşembe  │ [10:00] - [15:00]  [+]                │ ☑ Aktif     │
│ Cuma      │ [09:00] - [12:00]  [+]                │ ☑ Aktif     │
│ C.tesi    │ [Tüm gün kapalı]                      │ ☐ Kapalı    │
│ Pazar     │ [Tüm gün kapalı]                      │ ☐ Kapalı    │
├──────────┴────────────────────────────────────────┴─────────────┤
│ [Tüm günlere uygula]  [Şablonu Kaydet]                         │
└─────────────────────────────────────────────────────────────────┘
```

- Her gün için toggle (aktif/kapalı)
- Gün başına N adet zaman aralığı (+ ile ekleme, 🗑 ile silme)
- "Tüm günlere uygula" kısayolu (Calendly pattern)
- Minimum 30dk blok, 15dk granülerlik

#### Override Takvimi (Date-Specific)
- FullCalendar `dayGridMonth` görünümü
- Tarih tıklanınca modal açılır: "Bu gün için özel saatler" veya "Bu gün müsait değilim"
- Override olan tarihler takvimde farklı renk (turuncu badge)
- Tatil günleri toplu seçim (resmi tatiller listesi)

#### Drag-Select ile Slot Oluşturma
- FullCalendar `timeGridWeek` üzerinde `selectable: true`
- Mentor sürükleyerek yeni availability slot oluşturur
- `select` callback'i → Drawer açılır → "Bu aralığı müsait olarak kaydet?"
- Mevcut available slotlar yeşil event olarak gösterilir
- Booked slotlar mavi + kilitli ikon olarak gösterilir (drag/resize disabled)

#### Sürükle-Bırak ile Taşıma/Uzatma
- Sadece `IsBooked === false` olan slotlar taşınabilir/uzatılabilir
- `eventAllow` callback: `return !event.extendedProps.isBooked`
- Taşıma sonrası backend'e PUT request → overlap kontrolü
- Çakışma varsa: eski pozisyona snap-back + hata toast

### 2.2 Öğrenci: Slot Seçimi

**Calendly İlham:** Aylık takvim + sağdaki slot listesi.

#### Desktop Layout
```
┌──────────────────────────────┬──────────────────────────┐
│   📅 Şubat 2025              │  15 Şubat Cumartesi      │
│  ┌──┬──┬──┬──┬──┬──┬──┐     │                          │
│  │Pt│Sa│Ça│Pe│Cu│Ct│Pz│     │  ⏰ Müsait Saatler:      │
│  ├──┼──┼──┼──┼──┼──┼──┤     │                          │
│  │  │  │  │  │  │ 1│ 2│     │  [09:00 - 10:00]  ₺350  │
│  │ 3│ 4│ 5│ 6│ 7│ 8│ 9│     │  [10:00 - 11:00]  ₺350  │
│  │10│11│12│13│14│●15│16│     │  [14:00 - 15:00]  ₺350  │
│  │17│18│19│20│21│22│23│     │  [14:00 - 15:10]  ₺410  │
│  │24│25│26│27│28│  │  │     │                          │
│  └──┴──┴──┴──┴──┴──┴──┘     │  Süre: ○ 60dk  ○ 70dk   │
│                              │                          │
│  🌍 Saat dilimi: Europe/     │  [Devam Et →]            │
│     Istanbul (UTC+3)  [▾]    │                          │
└──────────────────────────────┴──────────────────────────┘
```

- Müsait günler: koyu metin + tıklanabilir
- Müsait olmayan günler: soluk, tıklanamaz (`dayCellClassNames`)
- Geçmiş tarihler: gizli/disabled
- Seçilen gün: primary renk vurgusu
- Slot listesi: zaman + fiyat gösteren butonlar
- Seçilen slot: primary renk + checkmark
- Fiyat, offering süresine göre orantılı hesaplanır
- Timezone selector: auto-detect + manual override
- Sadece müsait slotlar gösterilir (Calendly: "show only bookable")

#### Mobil Layout
```
┌────────────────────────┐
│ 📅 Şubat 2025    [◀ ▶] │
│ Pt Sa Ça Pe Cu Ct Pz   │
│     1  2  3  4  5  6   │
│  7  8  9 10 11 12 13   │
│ 14 ●15 16 17 18 19 20  │
│ 21 22 23 24 25 26 27   │
│ 28                      │
├────────────────────────┤
│ 15 Şubat Cumartesi     │
│ ┌──────────────────┐   │
│ │ 09:00 - 10:00    │   │
│ │ 60dk • ₺350      │   │
│ └──────────────────┘   │
│ ┌──────────────────┐   │
│ │ 10:00 - 11:00    │   │
│ │ 60dk • ₺350      │   │
│ └──────────────────┘   │
│ [Devam Et →]           │
└────────────────────────┘
```

- Takvim üstte (kompakt)
- Slot listesi altta (scroll)
- Swipe ile ay değişimi

### 2.3 Admin: Master Takvim

#### Filtre & Görünüm
- Sol panel: Mentor filtre (search + multi-select), Öğrenci filtre, Statü checkboxes, Ödeme durumu, Tarih range
- Üst toolbar: View switcher (Hafta / Ay / Liste) + Bugün butonu + Tarih navigasyon
- Ana alan: FullCalendar `timeGridWeek` (varsayılan)
- Sağ drawer: Seçilen event'in tam detayı

#### Renk Kodlaması (Admin Görünüm)
- Her mentor'a otomatik renk atanır (10 renklik palette)
- Statü overlay: opacity/border ile ayrıştırma
- Anomali eventleri: kırmızı çerçeve + uyarı ikonu

---

## 3. EVENT / SLOT VERİ MODELİ

### 3.1 AvailabilitySlot (Mentor Tanımlar)

```json
{
  "id": "a1b2c3d4-...",
  "mentorUserId": "m1n2o3p4-...",
  "startAt": "2025-02-15T09:00:00Z",
  "endAt": "2025-02-15T12:00:00Z",
  "timezone": "Europe/Istanbul",
  "isBooked": false,
  "recurrenceId": "r1s2t3u4-...",
  "recurrenceRule": "WEEKLY:MON,TUE,THU",
  "isOverride": false,
  "createdAt": "2025-02-01T10:00:00Z",
  "updatedAt": "2025-02-01T10:00:00Z"
}
```

**Yeni alanlar (mevcut entity'e eklenecek):**
- `timezone` (string): Mentor'un bu slot'u oluştururken kullandığı timezone. UTC dönüşümünde referans.
- `recurrenceRule` (string, nullable): Haftalık tekrar kuralı. `null` ise tek seferlik.
- `isOverride` (bool): Haftalık şablonu ezip özel saat mi?

### 3.2 AvailabilityTemplate (YENİ - Haftalık Şablon)

```json
{
  "id": "t1u2v3w4-...",
  "mentorUserId": "m1n2o3p4-...",
  "name": "Varsayılan Program",
  "timezone": "Europe/Istanbul",
  "isDefault": true,
  "rules": [
    { "dayOfWeek": 1, "startTime": "09:00", "endTime": "12:00", "isActive": true },
    { "dayOfWeek": 1, "startTime": "14:00", "endTime": "18:00", "isActive": true },
    { "dayOfWeek": 2, "startTime": "09:00", "endTime": "17:00", "isActive": true },
    { "dayOfWeek": 3, "startTime": null, "endTime": null, "isActive": false },
    { "dayOfWeek": 4, "startTime": "10:00", "endTime": "15:00", "isActive": true },
    { "dayOfWeek": 5, "startTime": "09:00", "endTime": "12:00", "isActive": true },
    { "dayOfWeek": 6, "startTime": null, "endTime": null, "isActive": false },
    { "dayOfWeek": 0, "startTime": null, "endTime": null, "isActive": false }
  ],
  "overrides": [
    { "date": "2025-02-19", "startTime": null, "endTime": null, "isBlocked": true, "reason": "Resmi tatil" },
    { "date": "2025-02-22", "startTime": "10:00", "endTime": "14:00", "isBlocked": false, "reason": "Özel saat" }
  ],
  "settings": {
    "minNoticeHours": 2,
    "maxBookingDaysAhead": 60,
    "bufferBeforeMin": 0,
    "bufferAfterMin": 15,
    "slotGranularityMin": 30,
    "maxBookingsPerDay": 5
  },
  "createdAt": "2025-02-01T10:00:00Z"
}
```

### 3.3 Booking (Öğrenci Oluşturur)

```json
{
  "id": "b1c2d3e4-...",
  "studentUserId": "s1t2u3v4-...",
  "mentorUserId": "m1n2o3p4-...",
  "offeringId": "o1p2q3r4-...",
  "availabilitySlotId": "a1b2c3d4-...",
  "startAt": "2025-02-15T09:00:00Z",
  "endAt": "2025-02-15T10:00:00Z",
  "durationMin": 60,
  "timezone": "Europe/Istanbul",
  "status": "Confirmed",
  "cancellationReason": null,
  "cancelledBy": null,
  "notes": "YKS Matematik stratejisi hakkında konuşmak istiyorum",
  "isEditable": false,
  "lockReason": "BookingConfirmed",
  "lockedBy": "System",
  "lockedUntil": null,
  "auditRequired": true,
  "createdAt": "2025-02-10T14:00:00Z",
  "updatedAt": "2025-02-10T14:30:00Z"
}
```

**Yeni alanlar:**
- `availabilitySlotId` (Guid): Hangi availability slot'unu kapladığını izler
- `timezone` (string): Booking oluşturulurken kullanılan timezone
- `cancelledBy` (string, nullable): "Student" | "Mentor" | "Admin" | "System"
- `notes` (string, nullable): Öğrenci'nin booking sorusu (Superpeer pattern)
- `isEditable` (bool): Frontend'in kontrol edeceği bayrak
- `lockReason` (string, nullable): Enum string değeri
- `lockedBy` (string, nullable): "System" | "Admin"
- `lockedUntil` (DateTime?, nullable): Geçici kilitler için

### 3.4 Order / Payment

```json
{
  "id": "p1q2r3s4-...",
  "buyerUserId": "s1t2u3v4-...",
  "type": "Booking",
  "resourceId": "b1c2d3e4-...",
  "amountTotal": 374.50,
  "amountBase": 350.00,
  "platformFee": 24.50,
  "currency": "TRY",
  "status": "Paid",
  "paymentProvider": "Iyzico",
  "providerPaymentId": "iyz_123456789",
  "checkoutToken": "tok_abc...",
  "paidAt": "2025-02-10T14:30:00Z",
  "refundedAt": null,
  "refundAmount": null,
  "refundPercentage": null,
  "createdAt": "2025-02-10T14:25:00Z"
}
```

**Yeni alanlar:**
- `amountBase` (decimal): Platform fee öncesi tutar
- `platformFee` (decimal): %7 platform komisyonu
- `paidAt` (DateTime?): Ödeme tamamlanma zamanı
- `refundedAt` (DateTime?): İade zamanı
- `refundAmount` (decimal?): İade tutarı
- `refundPercentage` (decimal?): İade yüzdesi (100 / 50 / 0)

### 3.5 VideoSession / MeetingSession

```json
{
  "id": "v1w2x3y4-...",
  "resourceType": "Booking",
  "resourceId": "b1c2d3e4-...",
  "provider": "Twilio",
  "roomName": "booking_b1c2d3e4",
  "status": "Completed",
  "scheduledStartAt": "2025-02-15T09:00:00Z",
  "actualStartAt": "2025-02-15T09:02:00Z",
  "actualEndAt": "2025-02-15T09:58:00Z",
  "totalDurationSec": 3360,
  "participants": [
    {
      "userId": "m1n2o3p4-...",
      "role": "Mentor",
      "joinedAt": "2025-02-15T09:00:00Z",
      "leftAt": "2025-02-15T09:58:00Z",
      "durationSec": 3480
    },
    {
      "userId": "s1t2u3v4-...",
      "role": "Student",
      "joinedAt": "2025-02-15T09:02:00Z",
      "leftAt": "2025-02-15T09:58:00Z",
      "durationSec": 3360
    }
  ],
  "createdAt": "2025-02-15T08:55:00Z"
}
```

**Yeni alanlar:**
- `scheduledStartAt` (DateTime): Planlanan başlama zamanı (booking.startAt)
- `actualStartAt` (DateTime?): Gerçek başlama (ilk katılımcı join)
- `actualEndAt` (DateTime?): Gerçek bitiş
- `totalDurationSec` (int): Toplam süre

---

## 4. STATUS MAKİNELERİ

### 4.1 BookingStatus State Machine

```
                    ┌──────────────────────────────────────────────┐
                    │                                              │
  ┌──────────┐     │  ┌───────────┐      ┌───────────┐           │
  │  (Yeni)  │────▶│  │  Pending  │─────▶│ Confirmed │           │
  └──────────┘     │  │  Payment  │      └─────┬─────┘           │
                    │  └─────┬─────┘            │                  │
                    │        │                  ├──────────────┐   │
                    │        │ 48h timeout      │              │   │
                    │        ▼                  ▼              ▼   │
                    │  ┌──────────┐      ┌────────────┐  ┌──────┐│
                    │  │ Expired  │      │ InProgress │  │Cancel││
                    │  └──────────┘      └──────┬─────┘  │  led ││
                    │                           │        └──┬───┘│
                    │                    ┌──────┴──────┐    │    │
                    │                    ▼             ▼    │    │
                    │             ┌───────────┐  ┌────────┐│    │
                    │             │ Completed │  │ NoShow ││    │
                    │             └─────┬─────┘  └───┬────┘│    │
                    │                   │            │     │    │
                    │                   ▼            ▼     │    │
                    │             ┌───────────┐           │    │
                    │             │ Disputed  │───────────┘    │
                    │             └─────┬─────┘                │
                    │                   │                      │
                    │                   ▼                      │
                    │             ┌───────────┐                │
                    │             │ Refunded  │                │
                    │             └───────────┘                │
                    └──────────────────────────────────────────┘
```

**Geçiş Kuralları:**

| Kaynak | Hedef | Tetikleyen | Koşul |
|--------|-------|------------|-------|
| PendingPayment | Confirmed | System (ödeme webhook) | Ödeme başarılı |
| PendingPayment | Expired | System (Hangfire job) | 48 saat içinde ödeme yapılmadı |
| Confirmed | InProgress | System | Seans başlama zamanı geldi + video room active |
| Confirmed | Cancelled | Student / Mentor / Admin | İptal talebi + refund kuralları uygulanır |
| InProgress | Completed | Mentor | Mentor seansı tamamladı |
| InProgress | NoShow | System / Mentor | Başlama zamanından 15dk sonra öğrenci katılmadı |
| Completed | Disputed | Student | Öğrenci itiraz etti (7 gün içinde) |
| NoShow | Disputed | Student | Öğrenci "ben katıldım" itirazı |
| Disputed | Completed | Admin | Admin mentör lehine karar |
| Disputed | Cancelled | Admin | Admin öğrenci lehine karar → refund |
| Cancelled | Refunded | System | Otomatik iade işlendi |

### 4.2 OrderStatus (PaymentStatus) State Machine

```
  ┌──────────┐
  │ Pending  │
  └────┬─────┘
       │
  ┌────┴────┐
  ▼         ▼
┌──────┐  ┌──────┐
│ Paid │  │Failed│
└──┬───┘  └──────┘
   │
   ├─────────────┐
   ▼             ▼
┌──────────┐  ┌───────────┐
│ Refunded │  │Chargeback │
└──────────┘  └───────────┘
```

| Kaynak | Hedef | Tetikleyen | Koşul |
|--------|-------|------------|-------|
| Pending | Paid | System (iyzico webhook) | Ödeme başarılı |
| Pending | Failed | System (iyzico webhook) | Ödeme başarısız / timeout |
| Paid | Refunded | System / Admin | Booking iptal → refund kuralı |
| Paid | Chargeback | System (iyzico webhook) | Kullanıcı bankadan iade talep etti |

### 4.3 VideoSessionStatus (MeetingStatus) State Machine

```
  ┌───────────┐
  │ Scheduled │
  └─────┬─────┘
        │
        ▼
  ┌───────────┐
  │RoomCreated│
  └─────┬─────┘
        │
        ▼
  ┌───────────┐
  │   Live    │
  └─────┬─────┘
        │
  ┌─────┴──────────────────┐
  ▼                        ▼
┌───────────────┐  ┌────────────────┐
│   Completed   │  │MentorLeftEarly │
└───────────────┘  └────────────────┘

        ┌──────────────────┐
        ▼                  │
  ┌───────────────┐        │
  │ StudentNoShow │        │
  └───────────────┘        │
                           │
  ┌───────────────┐        │
  │   Expired     │────────┘
  └───────────────┘
```

| Kaynak | Hedef | Tetikleyen | Koşul |
|--------|-------|------------|-------|
| Scheduled | RoomCreated | System | Twilio room oluşturuldu |
| Scheduled | Expired | System (Hangfire) | Başlama zamanından 30dk sonra hâlâ room yok |
| RoomCreated | Live | System (webhook) | İlk katılımcı bağlandı |
| Live | Completed | System/Mentor | Room kapatıldı, süre ≥ %70 |
| Live | MentorLeftEarly | System | Mentor planlanan sürenin <%50'sinde ayrıldı |
| Live | StudentNoShow | System | Mentor bağlandı, 15dk sonra öğrenci gelmedi |

### 4.4 Birleşik Görünüm: Statü → Görsel Davranış

| Booking Status | Takvim Rengi | Opaklık | Border | İkon | Tooltip |
|----------------|-------------|---------|--------|------|---------|
| PendingPayment | `#F59E0B` (amber) | 60% | dashed | ⏳ | "Ödeme bekleniyor" |
| Confirmed | `#227070` (teal) | 100% | solid | ✓ | "Onaylandı – {saat}" |
| InProgress | `#2563EB` (blue) | 100% | solid 2px | 🔴 | "Canlı – {katılımcılar}" |
| Completed | `#16A34A` (green) | 80% | solid | ✓✓ | "Tamamlandı – {süre}dk" |
| Cancelled | `#DC2626` (red) | 40% | dashed | ✕ | "İptal – {sebep}" |
| NoShow | `#7C3AED` (purple) | 60% | solid | 👻 | "Katılmadı" |
| Disputed | `#EA580C` (orange) | 80% | double | ⚠️ | "İtiraz – inceleniyor" |
| Expired | `#6B7280` (gray) | 30% | dotted | – | "Süresi doldu" |
| Refunded | `#6B7280` (gray) | 40% | dashed | ↩ | "İade edildi – ₺{tutar}" |
| Available (slot) | `#D1FAE5` (green-100) | 100% | solid | – | "Müsait – {saat aralığı}" |

---

## 5. UI KURALLARI

### 5.1 Renk/Durum Legend'i

```
┌─────────────────────────────────────────────────┐
│  Durum Göstergesi                               │
│  ■ Müsait        ■ Ödeme Bekliyor  ■ Onaylı    │
│  ■ Canlı         ■ Tamamlandı      ■ İptal     │
│  ■ Katılmadı     ■ İtiraz          ■ Süresi    │
│                                       Doldu     │
└─────────────────────────────────────────────────┘
```

- Legend her takvim görünümünde üst toolbar'ın sağında gösterilir
- Tıklanarak filtre işlevi görür (aktif/pasif toggle)
- Mobilde collapse edilir, "Filtreler" butonu ile açılır

### 5.2 Tooltip/Popover İçerik Standardı

#### Mentor Takviminde Tooltip
```
┌─────────────────────────────────┐
│ 📘 YKS Matematik Stratejisi    │
│ 👤 Ahmet Yılmaz (Öğrenci)      │
│ ⏰ 09:00 – 10:00 (60dk)        │
│ 💰 ₺350.00                     │
│ 📍 Durum: Onaylandı ✓          │
│ ─────────────────────────────── │
│ [Video Başlat] [Detay]  [İptal]│
└─────────────────────────────────┘
```

#### Öğrenci Takviminde Tooltip
```
┌─────────────────────────────────┐
│ 📘 YKS Matematik Stratejisi    │
│ 👨‍🏫 Dr. Elif Kaya (Mentor)     │
│ ⏰ 09:00 – 10:00 (60dk)        │
│ 💰 ₺350.00 – Ödendi ✓          │
│ 📍 Durum: Onaylandı            │
│ ─────────────────────────────── │
│ [Derse Katıl]  [Detay]  [İptal]│
└─────────────────────────────────┘
```

#### Admin Takviminde Tooltip
```
┌─────────────────────────────────┐
│ 📘 YKS Matematik Stratejisi    │
│ 👨‍🏫 Dr. Elif Kaya → Ahmet Y.   │
│ ⏰ 09:00 – 10:00 (60dk)        │
│ 💰 ₺350.00 / Ödeme: Paid       │
│ 📍 Booking: Confirmed          │
│ 🎥 Video: Scheduled            │
│ ─────────────────────────────── │
│ [Detay] [Override] [Audit Log] │
└─────────────────────────────────┘
```

### 5.3 Disabled/Readonly Kuralları

| Koşul | UI Davranışı | cursor | opacity |
|-------|-------------|--------|---------|
| Booked slot (mentor takvimi) | Taşınamaz, boyutlandırılamaz | `not-allowed` | 1.0 |
| Geçmiş tarih (tüm roller) | Tıklanabilir (detay) ama düzenlenemez | `default` | 0.6 |
| PendingPayment booking | Düzenlenemez, iptal edilebilir | `default` | 0.7 |
| InProgress booking | Düzenlenemez, sadece video + complete | `default` | 1.0 |
| Completed/Cancelled/Expired | Salt okunur, sadece detay görüntüleme | `default` | 0.5 |
| Başka mentor'un slot'u (admin) | Görüntülenebilir, override ile düzenlenebilir | `pointer` | 0.9 |
| Buffer time alanları | Görsel gösterim, tıklanamaz | `not-allowed` | 0.3 |

### 5.4 "Şu Sebeple Düzenlenemez" Mesajları

| lockReason | Kullanıcıya Mesaj (TR) |
|------------|----------------------|
| `BookingExists` | "Bu slotta aktif bir rezervasyon var. Önce rezervasyonu iptal edin." |
| `PaymentPending` | "Ödeme işlemi devam ediyor. Ödeme tamamlanana kadar değişiklik yapılamaz." |
| `PaymentCompleted` | "Ödeme tamamlandı. Değişiklik için iptal/iade sürecini başlatın." |
| `SessionInProgress` | "Ders devam ediyor. Ders bitene kadar değişiklik yapılamaz." |
| `SessionCompleted` | "Ders tamamlandı. Geçmiş dersler düzenlenemez." |
| `PastDate` | "Geçmiş tarihli slotlar düzenlenemez." |
| `MinNoticePeriod` | "Minimum bildirim süresi ({N} saat) geçti. Değişiklik yapılamaz." |
| `DisputeActive` | "Bu rezervasyonda aktif bir itiraz var. İtiraz çözülene kadar bekleyin." |
| `AdminLocked` | "Bu kayıt admin tarafından kilitlendi. Detay için destek ile iletişime geçin." |
| `RefundProcessing` | "İade işlemi devam ediyor." |
| `ConcurrencyConflict` | "Başka bir işlem devam ediyor. Lütfen sayfayı yenileyip tekrar deneyin." |

---

## 6. DÜZENLENEMEZ (LOCKED) SENARYOLARI

### 6A. Mentor Availability Slot

| # | Senaryo | Oluşum Koşulu | Kilitlenir mi? | lockReason | Kilitlenen İşlemler | Override Eden | Override Koşulu | UI Mesajı | Audit |
|---|---------|---------------|----------------|------------|---------------------|---------------|-----------------|-----------|-------|
| 1 | Slot'a booking yapıldı | Öğrenci ödeme tamamladı | ✅ Evet | `BookingExists` | move, resize, delete | Admin | Booking iptal edilmeli önce | "Aktif rezervasyon var" | ✅ |
| 2 | Slot geçmişte kaldı | `endAt < now()` | ✅ Evet | `PastDate` | move, resize, delete, edit | Kimse | – | "Geçmiş slot düzenlenemez" | ❌ |
| 3 | Ödeme süreci başladı | Order status = Pending | ⚠️ Kısmi | `PaymentPending` | delete | Admin | 30dk timeout beklenmeli | "Ödeme işlemi devam ediyor" | ✅ |
| 4 | Slot başlama zamanı < minNotice | `startAt - now() < minNoticeHours` | ⚠️ Kısmi | `MinNoticePeriod` | delete (öğrenci göremez) | Admin | – | "Min bildirim süresi geçti" | ❌ |
| 5 | Mentor pasif durumda (IsListed=false) | Admin unpublish yaptı | ❌ Hayır | – | – (slotlar görünmez) | Admin | Publish yapılmalı | "Profiliniz yayında değil" | ✅ |
| 6 | Çakışan slot oluşturma denemesi | Overlap var | ❌ (Oluşturulamaz) | `OverlapConflict` | create | – | Önceki slot silinmeli | "Bu zaman diliminde zaten müsaitsiniz" | ❌ |
| 7 | Haftalık şablon değişimi sırasında booking var | Şablon güncelleme | ⚠️ Kısmi | `BookingExists` | delete (booked olanlar korunur) | Admin | – | "Booking olan slotlar korundu" | ✅ |

### 6B. Booking

| # | Senaryo | Oluşum Koşulu | Kilitlenir mi? | lockReason | Kilitlenen İşlemler | Override Eden | Override Koşulu | UI Mesajı | Audit |
|---|---------|---------------|----------------|------------|---------------------|---------------|-----------------|-----------|-------|
| 1 | Ödeme bekleniyor | Status = PendingPayment | ⚠️ Kısmi | `PaymentPending` | move, price change | Student (iptal) | 48h timeout sonrası otomatik expire | "Ödeme bekleniyor" | ✅ |
| 2 | Ödeme tamamlandı / Onaylandı | Status = Confirmed | ✅ Evet | `PaymentCompleted` | move, resize, price change | Student/Mentor (iptal) | Refund kuralları uygulanır | "Ödeme tamamlandı" | ✅ |
| 3 | Ders başladı | Status = InProgress | ✅ Evet | `SessionInProgress` | move, resize, cancel, price | Mentor (complete) | Ders bitmeli | "Ders devam ediyor" | ✅ |
| 4 | Ders tamamlandı | Status = Completed | ✅ Evet | `SessionCompleted` | Tümü | Student (dispute 7gün) | 7 gün dispute süresi | "Ders tamamlandı" | ❌ |
| 5 | İptal edildi | Status = Cancelled | ✅ Evet | `BookingCancelled` | Tümü | Kimse | – | "İptal edildi" | ❌ |
| 6 | No-show | Status = NoShow | ✅ Evet | `NoShow` | Tümü | Student (dispute) | 48h dispute süresi | "Katılım sağlanmadı" | ✅ |
| 7 | İtiraz açık | Status = Disputed | ✅ Evet | `DisputeActive` | Tümü | Admin | Admin karar vermeli | "İtiraz inceleniyor" | ✅ |
| 8 | İade yapıldı | Status = Refunded | ✅ Evet | `Refunded` | Tümü | Kimse | – | "İade tamamlandı" | ❌ |
| 9 | Süresi doldu | Status = Expired | ✅ Evet | `Expired` | Tümü | Kimse | – | "Süre doldu" | ❌ |
| 10 | Min. iptal süresi geçti | `startAt - now() < 2h` | ⚠️ Kısmi | `MinNoticePeriod` | cancel (0% iade) | Admin | Full refund gerekli | "İptal süresi doldu (%0 iade)" | ✅ |

### 6C. Payment (Order)

| # | Senaryo | Oluşum Koşulu | Kilitlenir mi? | lockReason | Kilitlenen İşlemler | Override Eden | Override Koşulu | UI Mesajı | Audit |
|---|---------|---------------|----------------|------------|---------------------|---------------|-----------------|-----------|-------|
| 1 | Ödeme başarılı | Status = Paid | ✅ Evet | `PaymentCompleted` | amount change, delete | Admin | Refund process başlatılmalı | "Ödeme alındı" | ✅ |
| 2 | Ödeme başarısız | Status = Failed | ✅ Evet | `PaymentFailed` | – (booking expire edilir) | Student (retry) | Yeni order oluşturulabilir | "Ödeme başarısız" | ✅ |
| 3 | İade yapıldı | Status = Refunded | ✅ Evet | `Refunded` | Tümü | Kimse | – | "İade edildi" | ✅ |
| 4 | Chargeback | Status = Chargeback | ✅ Evet | `Chargeback` | Tümü | Kimse | İyzico/banka süreci | "Chargeback" | ✅ |
| 5 | Checkout açık (pending) | Status = Pending, token aktif | ⚠️ Kısmi | `CheckoutActive` | – | System (30dk timeout) | Timeout veya başarılı/başarısız | "İşlem devam ediyor" | ❌ |

### 6D. MeetingSession (Video Görüşme)

| # | Senaryo | Oluşum Koşulu | Kilitlenir mi? | lockReason | Kilitlenen İşlemler | Override Eden | Override Koşulu | UI Mesajı | Audit |
|---|---------|---------------|----------------|------------|---------------------|---------------|-----------------|-----------|-------|
| 1 | Oda aktif (Live) | Katılımcılar bağlı | ✅ Evet | `SessionLive` | booking cancel, move | Mentor (end) | End session çağrılmalı | "Video görüşme devam ediyor" | ✅ |
| 2 | Mentor erken ayrıldı | Mentor <%50 sürede çıktı | ✅ Evet | `MentorLeftEarly` | complete (auto dispute?) | Admin | – | "Mentor erken ayrıldı" | ✅ |
| 3 | Öğrenci gelmedi | 15dk sonra hâlâ katılmadı | ✅ Evet | `StudentNoShow` | – | Student (dispute) | 48h içinde itiraz | "Öğrenci katılmadı" | ✅ |
| 4 | Oda süresi doldu | 30dk sonra room oluşturulmadı | ✅ Evet | `Expired` | – | System | Otomatik expire | "Görüşme başlatılmadı" | ✅ |
| 5 | Tamamlandı | Room kapatıldı, süre yeterli | ✅ Evet | `SessionCompleted` | Tümü | Kimse | – | "Görüşme tamamlandı" | ❌ |

### 6E. Admin Override Senaryoları

| # | Senaryo | Oluşum Koşulu | Kilitlenen Entity | Override Aksiyonu | Koşul | UI Mesajı | Audit |
|---|---------|---------------|-------------------|-------------------|-------|-----------|-------|
| 1 | Ödeme sorunu çözümü | Ödeme var ama booking oluşmamış | Booking + Order | Manuel booking confirm | Ödeme kanıtı doğrulanmalı | "Admin tarafından onaylandı" | ✅ Zorunlu |
| 2 | Zorla iptal | Mentor/öğrenci şikayet | Booking | Force cancel + refund | Admin onayı zorunlu | "Admin tarafından iptal edildi" | ✅ Zorunlu |
| 3 | Zaman değişikliği | Karşılıklı anlaşma | Booking + AvailabilitySlot | Move booking to new slot | Her iki tarafın onayı veya admin kararı | "Tarih/saat değiştirildi" | ✅ Zorunlu |
| 4 | Fiyat düzeltme | Hatalı fiyatlandırma | Order | Amount güncelleme | Fark iade/tahsil edilmeli | "Fiyat düzeltildi" | ✅ Zorunlu |
| 5 | Dispute çözümü | İtiraz süreci | Booking + Order | Resolve: StudentFavor/MentorFavor | Kanıtlar incelenmeli | "İtiraz çözüldü" | ✅ Zorunlu |
| 6 | Mentor ban/unpublish | Kural ihlali | MentorProfile + tüm slotlar | Unpublish + aktif bookingleri iptal | Gerekçe zorunlu | "Mentor yayından kaldırıldı" | ✅ Zorunlu |
| 7 | Slot override | Acil durum | AvailabilitySlot | Force delete/modify | Etkilenen bookingler bilgilendirilmeli | "Admin tarafından düzenlendi" | ✅ Zorunlu |

---

## 7. STATUS-BAZLI KİLİT MATRİSİ

### 7.1 BookingStatus Matrisi

| Status | Düzenlenebilir? | Yasak İşlemler | Kilit Tipi | Sistem Otomatik Aksiyonu | UI Renk | UI Davranış |
|--------|----------------|----------------|------------|--------------------------|---------|-------------|
| **PendingPayment** | ⚠️ Kısmi | move, resize, price change | Otomatik | 48h sonra → Expired. Slot serbest bırakılır. | Amber `#F59E0B` | Dashed border, 60% opacity, ⏳ ikon |
| **Confirmed** | ❌ Hayır | move, resize, price change | Otomatik | Başlama zamanında → InProgress (video join varsa). Reminder: 24h + 15dk önce. | Teal `#227070` | Solid border, full opacity, ✓ ikon |
| **InProgress** | ❌ Hayır | move, resize, cancel, price | Otomatik | Planlanan bitiş + 15dk sonra → Complete (auto). Mentor 15dk yoksa → MentorLeftEarly. | Blue `#2563EB` | Solid 2px, pulse animation, 🔴 ikon |
| **Completed** | ❌ Hayır | Tüm düzenleme | Otomatik | Mentor escrow → available (7 gün sonra, dispute yoksa). Feedback email gönder. | Green `#16A34A` | Solid border, 80% opacity, ✓✓ ikon |
| **Cancelled** | ❌ Hayır | Tüm düzenleme | Otomatik | Refund kuralları: 24h+ = %100, 2-24h = %50, <2h = %0. Slot serbest bırakılır. | Red `#DC2626` | Dashed border, 40% opacity, ✕ ikon, strikethrough |
| **NoShow** | ❌ Hayır | Tüm düzenleme | Otomatik | Mentor'a tam ödeme. Öğrenci'ye no-show bildirimi. 48h dispute penceresi. | Purple `#7C3AED` | Solid border, 60% opacity, 👻 ikon |
| **Expired** | ❌ Hayır | Tüm düzenleme | Otomatik | Slot serbest bırakılır. Order → Failed. Bildirim gönder. | Gray `#6B7280` | Dotted border, 30% opacity |
| **Disputed** | ❌ Hayır | Tüm düzenleme | Otomatik | Admin'e bildirim. Mentor ödemesi dondurulur. 72h SLA. | Orange `#EA580C` | Double border, 80% opacity, ⚠️ ikon |
| **Refunded** | ❌ Hayır | Tüm düzenleme | Otomatik | Ledger entry: StudentRefund Credit. | Gray `#6B7280` | Dashed border, 40% opacity, ↩ ikon |

### 7.2 OrderStatus (PaymentStatus) Matrisi

| Status | Düzenlenebilir? | Yasak İşlemler | Kilit Tipi | Sistem Otomatik Aksiyonu | UI Renk | UI Davranış |
|--------|----------------|----------------|------------|--------------------------|---------|-------------|
| **Pending** | ⚠️ Kısmi (retry) | amount change | Otomatik | 30dk checkout timeout. Booking slot reserved. | Amber | Spinning indicator, "İşleniyor..." |
| **Paid** | ❌ Hayır | amount, delete | Otomatik | Booking → Confirmed. Ledger: MentorEscrow + Platform. | Green | Checkmark, "₺{tutar} ödendi" |
| **Failed** | ❌ Hayır (yeni order) | – | Otomatik | Booking → Expired (48h sonra). Slot serbest. Retry bildirimi. | Red | ✕ ikon, "Ödeme başarısız – tekrar dene" |
| **Refunded** | ❌ Hayır | Tümü | Manuel/Oto | Ledger: StudentRefund Credit, MentorEscrow Debit. | Gray | ↩ ikon, "₺{tutar} iade edildi" |
| **Chargeback** | ❌ Hayır | Tümü | Otomatik | Admin'e uyarı. Mentor ödemesi dondurulur. İnceleme başlatılır. | Dark Red | ⚠️ ikon, "Chargeback – inceleniyor" |

### 7.3 VideoSessionStatus (MeetingStatus) Matrisi

| Status | Düzenlenebilir? | Yasak İşlemler | Kilit Tipi | Sistem Otomatik Aksiyonu | UI Renk | UI Davranış |
|--------|----------------|----------------|------------|--------------------------|---------|-------------|
| **Scheduled** | ⚠️ Kısmi | – | Otomatik | Başlama zamanı - 5dk: Room auto-create. | Gray | "Planlandı", clock ikon |
| **RoomCreated** | ❌ Hayır | booking cancel | Otomatik | 30dk boyunca katılım yoksa → Expired. | Blue-gray | "Oda hazır", link göster |
| **Live** | ❌ Hayır | booking cancel, move | Otomatik | Planlanan bitiş + 15dk → auto-end. Her 5dk duration güncelle. | Blue pulse | "Canlı 🔴", participant count |
| **MentorLeftEarly** | ❌ Hayır | Tümü | Otomatik | Auto-dispute oluştur. Admin'e bildirim. | Orange | ⚠️ "Mentor erken ayrıldı" |
| **StudentNoShow** | ❌ Hayır | Tümü | Otomatik | Booking → NoShow. Mentor'a tam ödeme. | Purple | 👻 "Öğrenci katılmadı" |
| **Completed** | ❌ Hayır | Tümü | Otomatik | Duration hesapla. Booking → Complete tetikle. | Green | ✓ "{süre}dk görüşme" |
| **Expired** | ❌ Hayır | Tümü | Otomatik | Admin'e anomali bildirimi. İnceleme gerekli. | Gray | "Görüşme başlatılmadı" |

---

## 8. CONCURRENCY & KRİTİK EDGE-CASE SENARYOLARI

### 8.1 Çift Rezervasyon (Double Booking)

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 1 | **Aynı slota 2 öğrenci aynı anda booking** | İlk gelen kazanır. `SELECT FOR UPDATE` ile satır kilidi. İkinci deneme → "Slot artık müsait değil" | Row-level lock `AvailabilitySlot` | "Bu saat aralığı az önce başka bir öğrenci tarafından rezerve edildi. Lütfen başka bir saat seçin." | `BEGIN → SELECT slot FOR UPDATE → check isBooked → INSERT booking → UPDATE slot.isBooked = true → COMMIT` | ✅ Her iki deneme loglanır |
| 2 | **Aynı slota 2 ödeme aynı anda tamamlandı** | Idempotency: ilk webhook işlenir, ikinci → "already processed" | Order.CheckoutToken unique constraint | – (kullanıcıya görünmez) | `BEGIN → SELECT order WHERE token = @token → if Paid RETURN → UPDATE order → COMMIT` | ✅ |
| 3 | **Mentor aynı saate 2 availability slot oluşturma** | Overlap kontrolü. Reddet. | Application validation | "Bu zaman diliminde zaten bir müsaitlik tanımınız var." | `SELECT WHERE mentor AND overlap → if exists THROW` | ❌ |

### 8.2 Ödeme-Booking Uyumsuzlukları

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 4 | **Ödeme başarılı ama booking oluşmadı** | Webhook handler'da retry (3x). Başarısızsa anomali tablosuna yaz. Admin dashboard'da göster. | Order locked as Paid, booking'siz | "Ödemeniz alındı, rezervasyonunuz işleniyor. En kısa sürede onay alacaksınız." | `Transaction: Order.Paid → Booking.Confirm → Slot.MarkAsBooked` hepsi tek transaction | ✅ Zorunlu |
| 5 | **Booking oluştu ama ödeme başarısız** | Booking → PendingPayment kalır. 48h sonra → Expired. Slot serbest. | Booking soft-locked (PendingPayment) | "Ödeme işleminiz başarısız oldu. 48 saat içinde tekrar deneyebilirsiniz." | Hangfire job: 48h sonra `ExpireBookingJob` | ✅ |
| 6 | **Ödeme sırasında slot başkası tarafından alındı** | CreateOrder anında slot re-check. Eğer booked ise → order oluşturma, hata dön. | Pre-order slot validation | "Seçtiğiniz zaman aralığı artık müsait değil. Lütfen farklı bir saat seçin." | `BEGIN → SELECT slot FOR UPDATE → if isBooked THROW → CREATE order → COMMIT` | ✅ |
| 7 | **Kısmi ödeme (tutarsızlık)** | iyzico tutarı vs order tutarı karşılaştır. Eşleşmezse → Order.Failed + admin alert | Order locked | "Ödeme tutarında uyuşmazlık tespit edildi. Destek ekibimiz sizinle iletişime geçecek." | Tutar kontrolü webhook handler'da | ✅ Zorunlu |
| 8 | **Çift ödeme (aynı booking için 2 order)** | ResourceId + Type unique constraint. İkinci order oluşturulamaz. | DB constraint | "Bu rezervasyon için zaten bir ödeme işlemi mevcut." | `UNIQUE INDEX (Type, ResourceId) WHERE Status != Failed` | ✅ |

### 8.3 Zamanlama & Timezone Sorunları

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 9 | **12:30 seçildi, DB'de 15:30 oldu (timezone kayması)** | Tüm zamanlar UTC olarak saklanır. Frontend IANA timezone gönderir (`Europe/Istanbul`). Backend `TimeZoneInfo.ConvertTimeToUtc()` ile dönüşüm. | N/A | – (doğru timezone gösterilir) | Request: `{ startAt: "2025-02-15T12:30:00", timezone: "Europe/Istanbul" }` → DB: `2025-02-15T09:30:00Z` | ❌ |
| 10 | **DST (Yaz saati) değişimi – 29 Mart 2025** | Saat 03:00'te ileri alınır (02:00→03:00 atlanır). UTC bazlı saklama bu sorunu ortadan kaldırır. Slot üretimi sırasında DST kontrolü. | N/A | "Not: 29 Mart'ta yaz saati uygulamasına geçilecektir. Saatler otomatik güncellenir." | `NodaTime` veya `TimeZoneInfo` ile DST-aware dönüşüm | ❌ |
| 11 | **Farklı timezone'daki mentor-öğrenci** | Her kullanıcı kendi timezone'unda görür. Backend UTC, frontend dönüştürür. | N/A | Tooltip: "🌍 Sizin saatiniz: 12:30 (UTC+3)" | Frontend: `Intl.DateTimeFormat` + stored timezone | ❌ |
| 12 | **Gece yarısı geçişinde slot bölünmesi** | Slot 23:00-01:00 → iki güne yayılır. Takvimde tek event olarak gösterilir. | N/A | Takvimde 23:00-01:00 tek blok | Backend: startAt < endAt validation (UTC bazında) | ❌ |

### 8.4 Video/Meeting Sorunları

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 13 | **Video token üretildi ama oda açılmadı** | CreateVideoSession'da Twilio API hatası → retry 3x. Başarısızsa → "Teknik sorun" + admin alert. | Booking unlocked (video retry) | "Video bağlantısı kurulamadı. Lütfen tekrar deneyin veya destek ile iletişime geçin." | Twilio circuit breaker pattern | ✅ |
| 14 | **Mentor bağlandı, öğrenci 15dk gelmedi** | Hangfire delayed job: booking.StartAt + 15dk → check participants. Öğrenci yoksa → NoShow. | Booking → NoShow locked | "Öğrenci 15 dakika içinde katılmadı. Ders 'Katılmadı' olarak işaretlendi." | `Job: CheckStudentNoShow(bookingId)` | ✅ |
| 15 | **Öğrenci bağlandı, mentor hiç gelmedi** | booking.StartAt + 15dk → mentor yoksa → otomatik dispute + full refund. | Booking → Disputed locked | "Mentor katılmadı. Otomatik itiraz oluşturuldu ve ödemeniz iade edilecek." | `Job: CheckMentorNoShow(bookingId) → Auto-Dispute` | ✅ Zorunlu |
| 16 | **Mentor planlanan sürenin %50'sinden önce ayrıldı** | Webhook: participant-disconnected + süre kontrolü. MentorLeftEarly → auto dispute trigger. | VideoSession → MentorLeftEarly | "Mentor dersi erken sonlandırdı. Kısmi iade için itiraz açabilirsiniz." | `OnMentorDisconnect: if duration < planned * 0.5 → MentorLeftEarly` | ✅ Zorunlu |
| 17 | **İnternet koptu, 5dk sonra yeniden bağlandı** | Twilio reconnect otomatik. Participant.LeftAt güncellenmez (grace period 2dk). | N/A | – (otomatik yeniden bağlanma) | Grace period: 2dk disconnect → don't mark as left | ❌ |

### 8.5 İptal & İade Sorunları

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 18 | **Mentor zaman değiştirirken öğrenci ödeme yaptı** | Slot availability re-check ödeme anında. Ödeme → slot artık yok → refund + "slot değişti" bildirimi. | Optimistic concurrency (rowversion) | "Seçtiğiniz saat aralığı değiştirildi. Ödemeniz iade edilecek." | `ConcurrencyToken on AvailabilitySlot` | ✅ Zorunlu |
| 19 | **Refund sürecinde yeniden rezervasyon denendi** | Booking Cancelled/Refunded statüsünde → aynı slot tekrar booking'e açılır ama aynı öğrenci-mentor-slot kombinasyonu engellenir (5dk cooldown). | Cooldown period | "İade işleminiz devam ediyor. Birkaç dakika sonra tekrar deneyebilirsiniz." | 5dk cooldown check | ❌ |
| 20 | **İptal sırasında ödeme webhook'u geldi** | Race condition. Booking.Cancel() + Order.Paid() çakışması. Transaction isolation. İptal kazanır → auto-refund. | Pessimistic lock on booking | "Rezervasyonunuz iptal edildi. Ödemeniz otomatik olarak iade edilecek." | `SERIALIZABLE isolation for cancel+payment overlap` | ✅ Zorunlu |
| 21 | **Kısmi iade sonrası tam iade talebi** | Admin kararı gerekli. Fark hesaplanır. | Admin-only override | "İade talebiniz inceleniyor." | Admin panel'de fark tutarı gösterilir | ✅ Zorunlu |

### 8.6 Cache & UI Sorunları

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 22 | **Admin iptal etti ama frontend cache stale** | React Query invalidation: booking mutation sonrası `queryClient.invalidateQueries(['bookings'])`. Ayrıca WebSocket/SSE ile real-time update. | N/A | Toast: "Bu rezervasyon güncellendi. Sayfa yenileniyor..." | `staleTime: 30s` for booking queries. Mutation → invalidate. | ❌ |
| 23 | **2 tab açık, birinde slot silindi diğerinde hâlâ görünüyor** | `BroadcastChannel API` ile tab-arası senkronizasyon. Veya refetchOnFocus. | N/A | – (otomatik güncelleme) | `refetchOnWindowFocus: true` in React Query config | ❌ |
| 24 | **Slot listesi yüklenirken mentor yeni slot ekledi** | Optimistic update + background refetch. Yeni slot sonraki fetch'te görünür (30s staleTime). | N/A | – | React Query background refetch | ❌ |
| 25 | **Yoğun trafik: 50 öğrenci aynı mentor'un 3 slotunu görüyor** | Redis cache: mentor availability 30s TTL. Booking oluşturulunca cache invalidate. Race'de sadece 3 kişi başarılı, 47 kişiye "slot doldu". | Redis distributed lock | "Bu saat doldu. Diğer müsait saatlere göz atın." | `Redis SETNX` for slot locking during checkout | ✅ |

### 8.7 Sistem & Altyapı

| # | Senaryo | Sistem Davranışı | Kilit Politikası | Kullanıcı Mesajı | Backend Transaction | Audit |
|---|---------|------------------|------------------|------------------|---------------------|-------|
| 26 | **Hangfire job başarısız (expire job çalışmadı)** | Retry policy: 3 attempt, exponential backoff. Dead letter → admin alert. | Booking stale state | – (admin müdahale) | `[AutomaticRetry(Attempts = 3)]` | ✅ |
| 27 | **DB connection timeout booking sırasında** | Transaction rollback. Slot serbest kalır. Retry prompt. | Transaction rollback = no lock | "İşlem zaman aşımına uğradı. Lütfen tekrar deneyin." | `TransactionScope + timeout` | ✅ |
| 28 | **İyzico webhook gecikmeli geldi (1 saat sonra)** | Idempotency check. Order hâlâ Pending ise → process. Expired ise → refund + admin alert. | Idempotent processing | "Ödemeniz gecikmeli olarak işlendi." (veya otomatik iade) | `Check order.Status before processing` | ✅ Zorunlu |
| 29 | **Mentor hesabı askıya alındı, aktif bookingleri var** | Tüm future bookingleri iptal → full refund. Availability slotları kaldırılır. | All mentor resources locked | "Mentor hesabı askıya alındı. Rezervasyonunuz iptal edildi ve ödemeniz iade edilecek." | Bulk cancel + bulk refund transaction | ✅ Zorunlu |
| 30 | **Rate limiting: Öğrenci 1dk'da 20 booking denemesi** | API rate limiter: 5 booking/dk/kullanıcı. Aşımda 429 Too Many Requests. | N/A | "Çok fazla istek gönderdiniz. Lütfen bir dakika bekleyin." | `AspNetCoreRateLimit` middleware | ✅ |

---

## 9. LOCK MODELİ ÖNERİSİ

### 9.1 Booking Entity Genişletme

```csharp
// Mevcut Booking entity'sine eklenecek alanlar
public class Booking : BaseEntity
{
    // ... mevcut alanlar ...

    // === LOCK MODEL ===
    public bool IsEditable { get; private set; } = true;
    public string? LockReason { get; private set; }
    public string? LockedBy { get; private set; }        // "System" | "Admin" | "Mentor" | "Student"
    public DateTime? LockedUntil { get; private set; }    // Geçici kilitler için
    public bool AuditRequired { get; private set; }

    // Lock methods
    public void Lock(string reason, string lockedBy, DateTime? until = null, bool auditRequired = true)
    {
        IsEditable = false;
        LockReason = reason;
        LockedBy = lockedBy;
        LockedUntil = until;
        AuditRequired = auditRequired;
    }

    public void Unlock()
    {
        IsEditable = true;
        LockReason = null;
        LockedBy = null;
        LockedUntil = null;
    }

    // Status transition methods (lock otomatik uygulanır)
    public new void Confirm()
    {
        Status = BookingStatus.Confirmed;
        Lock("PaymentCompleted", "System", auditRequired: true);
    }

    public void StartSession()
    {
        Status = BookingStatus.InProgress;
        Lock("SessionInProgress", "System", auditRequired: true);
    }
}
```

### 9.2 LockReason Enum

```csharp
public static class LockReasons
{
    // Booking locks
    public const string PaymentPending = "PaymentPending";
    public const string PaymentCompleted = "PaymentCompleted";
    public const string SessionInProgress = "SessionInProgress";
    public const string SessionCompleted = "SessionCompleted";
    public const string BookingCancelled = "BookingCancelled";
    public const string NoShow = "NoShow";
    public const string DisputeActive = "DisputeActive";
    public const string Expired = "Expired";
    public const string Refunded = "Refunded";
    public const string MinNoticePeriod = "MinNoticePeriod";
    public const string AdminLocked = "AdminLocked";
    public const string ConcurrencyConflict = "ConcurrencyConflict";
    public const string RefundProcessing = "RefundProcessing";

    // Availability slot locks
    public const string BookingExists = "BookingExists";
    public const string PastDate = "PastDate";
    public const string OverlapConflict = "OverlapConflict";
    public const string CheckoutActive = "CheckoutActive";

    // Video locks
    public const string SessionLive = "SessionLive";
    public const string MentorLeftEarly = "MentorLeftEarly";
    public const string StudentNoShow = "StudentNoShow";
}
```

### 9.3 Örnek API Response

```json
{
  "id": "b1c2d3e4-5678-9abc-def0-123456789abc",
  "studentUserId": "s1t2u3v4-...",
  "studentName": "Ahmet Yılmaz",
  "mentorUserId": "m1n2o3p4-...",
  "mentorName": "Dr. Elif Kaya",
  "mentorAvatar": "https://r2.degisimmentorluk.com/avatars/elif.jpg",
  "offeringId": "o1p2q3r4-...",
  "offeringTitle": "YKS Matematik Stratejisi",
  "startAt": "2025-02-15T09:00:00Z",
  "endAt": "2025-02-15T10:00:00Z",
  "durationMin": 60,
  "timezone": "Europe/Istanbul",
  "status": "Confirmed",
  "price": 350.00,
  "currency": "TRY",
  "notes": "AYT Matematik stratejisi hakkında konuşmak istiyorum",
  "cancellationReason": null,
  "cancelledBy": null,

  "lock": {
    "isEditable": false,
    "reason": "PaymentCompleted",
    "reasonMessage": "Ödeme tamamlandı. Değişiklik için iptal/iade sürecini başlatın.",
    "lockedBy": "System",
    "lockedUntil": null,
    "auditRequired": true
  },

  "allowedActions": [
    "view",
    "cancel",
    "startVideo"
  ],
  "disabledActions": {
    "move": "Ödeme tamamlandı. Tarih/saat değişikliği yapılamaz.",
    "resize": "Ödeme tamamlandı. Süre değişikliği yapılamaz.",
    "priceChange": "Ödeme tamamlandı. Fiyat değişikliği yapılamaz."
  },

  "payment": {
    "orderId": "p1q2r3s4-...",
    "status": "Paid",
    "amount": 374.50,
    "paidAt": "2025-02-10T14:30:00Z"
  },

  "videoSession": {
    "sessionId": null,
    "roomName": null,
    "status": "Scheduled"
  },

  "createdAt": "2025-02-10T14:00:00Z",
  "updatedAt": "2025-02-10T14:30:00Z"
}
```

### 9.4 AvailabilitySlot Lock Response

```json
{
  "id": "a1b2c3d4-...",
  "mentorUserId": "m1n2o3p4-...",
  "startAt": "2025-02-15T09:00:00Z",
  "endAt": "2025-02-15T12:00:00Z",
  "timezone": "Europe/Istanbul",
  "isBooked": true,

  "lock": {
    "isEditable": false,
    "reason": "BookingExists",
    "reasonMessage": "Bu slotta aktif bir rezervasyon var. Önce rezervasyonu iptal edin.",
    "lockedBy": "System"
  },

  "allowedActions": ["view"],
  "disabledActions": {
    "move": "Aktif rezervasyon var. Slot taşınamaz.",
    "resize": "Aktif rezervasyon var. Süre değiştirilemez.",
    "delete": "Aktif rezervasyon var. Slot silinemez."
  },

  "booking": {
    "bookingId": "b1c2d3e4-...",
    "studentName": "Ahmet Yılmaz",
    "status": "Confirmed"
  }
}
```

---

## 10. FULLCALENDAR ENTEGRASYON STRATEJİSİ

### 10.1 selectAllow — Kural Bazlı Slot Oluşturma

```typescript
// Mentor availability editor: sadece belirli koşullarda yeni slot oluşturulabilir
const selectAllow = (selectInfo: DateSelectArg): boolean => {
  const now = new Date();
  const start = selectInfo.start;
  const end = selectInfo.end;

  // Kural 1: Geçmiş tarihe slot oluşturulamaz
  if (start < now) return false;

  // Kural 2: Minimum 30dk
  const durationMs = end.getTime() - start.getTime();
  if (durationMs < 30 * 60 * 1000) return false;

  // Kural 3: Maksimum 8 saat
  if (durationMs > 8 * 60 * 60 * 1000) return false;

  // Kural 4: maxBookingDaysAhead kontrolü
  const maxDays = mentorSettings.maxBookingDaysAhead || 60;
  const maxDate = new Date();
  maxDate.setDate(maxDate.getDate() + maxDays);
  if (start > maxDate) return false;

  // Kural 5: Overlap kontrolü (mevcut slotlarla çakışma)
  const overlapping = calendarApi.getEvents().some(event => {
    if (event.extendedProps.type !== 'availability') return false;
    return start < event.end! && end > event.start!;
  });
  if (overlapping) return false;

  return true;
};
```

### 10.2 eventAllow — Lock Kontrolü

```typescript
// Drag & resize izin kontrolü
const eventAllow = (dropInfo: DateSpanApi, draggedEvent: EventApi): boolean => {
  const props = draggedEvent.extendedProps;

  // Kural 1: Booked slotlar taşınamaz
  if (props.isBooked) return false;

  // Kural 2: Lock'lu eventler taşınamaz
  if (props.lock?.isEditable === false) return false;

  // Kural 3: Geçmiş tarihe taşınamaz
  if (dropInfo.start < new Date()) return false;

  // Kural 4: Sadece availability slotları taşınabilir (booking taşınamaz)
  if (props.type === 'booking') return false;

  // Kural 5: Overlap kontrolü
  const calendarApi = dropInfo.view.calendar;
  const overlapping = calendarApi.getEvents().some(event => {
    if (event.id === draggedEvent.id) return false;
    if (event.extendedProps.type !== 'availability') return false;
    return dropInfo.start < event.end! && dropInfo.end! > event.start!;
  });
  if (overlapping) return false;

  return true;
};
```

### 10.3 eventClassNames — Status Renk Bağlama

```typescript
const eventClassNames = (arg: EventContentArg): string[] => {
  const props = arg.event.extendedProps;
  const classes: string[] = [];

  // Tip bazlı base class
  if (props.type === 'availability') {
    classes.push('fc-event-availability');
    classes.push(props.isBooked ? 'fc-event-booked' : 'fc-event-available');
  }

  if (props.type === 'booking') {
    classes.push('fc-event-booking');

    // Status bazlı renk
    switch (props.status) {
      case 'PendingPayment':
        classes.push('fc-status-pending-payment'); // amber, dashed, 60%
        break;
      case 'Confirmed':
        classes.push('fc-status-confirmed'); // teal, solid, 100%
        break;
      case 'InProgress':
        classes.push('fc-status-in-progress'); // blue, solid 2px, pulse
        break;
      case 'Completed':
        classes.push('fc-status-completed'); // green, solid, 80%
        break;
      case 'Cancelled':
        classes.push('fc-status-cancelled'); // red, dashed, 40%, strikethrough
        break;
      case 'NoShow':
        classes.push('fc-status-no-show'); // purple, solid, 60%
        break;
      case 'Disputed':
        classes.push('fc-status-disputed'); // orange, double border, 80%
        break;
      case 'Expired':
        classes.push('fc-status-expired'); // gray, dotted, 30%
        break;
      case 'Refunded':
        classes.push('fc-status-refunded'); // gray, dashed, 40%
        break;
    }
  }

  // Lock durumu
  if (props.lock?.isEditable === false) {
    classes.push('fc-event-locked');
  }

  return classes;
};
```

### 10.4 eventContent — Kilit İkonu ve Custom Render

```typescript
const eventContent = (arg: EventContentArg): React.ReactNode => {
  const props = arg.event.extendedProps;
  const isLocked = props.lock?.isEditable === false;

  // === AVAILABILITY SLOT ===
  if (props.type === 'availability') {
    return (
      <div className="fc-custom-event p-1">
        <div className="flex items-center gap-1">
          {props.isBooked && <Lock className="w-3 h-3 text-gray-500" />}
          <span className="text-xs font-medium truncate">
            {props.isBooked ? 'Rezerve' : 'Müsait'}
          </span>
        </div>
        {props.isBooked && props.booking && (
          <div className="text-[10px] text-gray-600 truncate">
            {props.booking.studentName}
          </div>
        )}
      </div>
    );
  }

  // === BOOKING EVENT ===
  if (props.type === 'booking') {
    const statusIcons: Record<string, string> = {
      PendingPayment: '⏳',
      Confirmed: '✓',
      InProgress: '🔴',
      Completed: '✓✓',
      Cancelled: '✕',
      NoShow: '👻',
      Disputed: '⚠️',
      Expired: '–',
      Refunded: '↩',
    };

    return (
      <div className="fc-custom-event p-1">
        <div className="flex items-center gap-1">
          {isLocked && <Lock className="w-3 h-3" />}
          <span className="text-[10px]">{statusIcons[props.status] || ''}</span>
          <span className="text-xs font-medium truncate">
            {arg.event.title}
          </span>
        </div>
        <div className="text-[10px] opacity-75 truncate">
          {props.personName} • {props.durationMin}dk
        </div>
        {props.status === 'InProgress' && (
          <div className="flex items-center gap-1 mt-0.5">
            <span className="w-1.5 h-1.5 bg-red-500 rounded-full animate-pulse" />
            <span className="text-[9px] font-medium">Canlı</span>
          </div>
        )}
      </div>
    );
  }

  return null;
};
```

### 10.5 Override Görünümü

Admin override yapıldığında event'te görsel fark:

```css
/* Override edilmiş event */
.fc-event-overridden {
  position: relative;
}

.fc-event-overridden::after {
  content: '🔧';
  position: absolute;
  top: 2px;
  right: 2px;
  font-size: 10px;
}

.fc-event-overridden .fc-event-main {
  border-left: 3px solid #F59E0B; /* Gold accent */
}

/* Lock ikonu pulse animasyonu */
.fc-event-locked {
  cursor: not-allowed !important;
}

.fc-event-locked:hover::before {
  content: attr(data-lock-message);
  position: absolute;
  /* tooltip positioning */
}
```

### 10.6 CSS Status Sınıfları

```css
/* === AVAILABILITY === */
.fc-event-available {
  background-color: #D1FAE5 !important; /* green-100 */
  border-color: #16A34A !important;
  color: #166534 !important;
}

.fc-event-booked {
  background-color: #DBEAFE !important; /* blue-100 */
  border-color: #2563EB !important;
  color: #1E40AF !important;
  cursor: not-allowed;
}

/* === BOOKING STATUSES === */
.fc-status-pending-payment {
  background-color: rgba(245, 158, 11, 0.6) !important;
  border: 1px dashed #F59E0B !important;
}

.fc-status-confirmed {
  background-color: #227070 !important;
  border: 1px solid #1a5656 !important;
  color: white !important;
}

.fc-status-in-progress {
  background-color: #2563EB !important;
  border: 2px solid #1D4ED8 !important;
  color: white !important;
  animation: pulse-border 2s infinite;
}

@keyframes pulse-border {
  0%, 100% { box-shadow: 0 0 0 0 rgba(37, 99, 235, 0.4); }
  50% { box-shadow: 0 0 0 4px rgba(37, 99, 235, 0); }
}

.fc-status-completed {
  background-color: rgba(22, 163, 74, 0.8) !important;
  border: 1px solid #16A34A !important;
  color: white !important;
}

.fc-status-cancelled {
  background-color: rgba(220, 38, 38, 0.4) !important;
  border: 1px dashed #DC2626 !important;
  text-decoration: line-through;
}

.fc-status-no-show {
  background-color: rgba(124, 58, 237, 0.6) !important;
  border: 1px solid #7C3AED !important;
  color: white !important;
}

.fc-status-disputed {
  background-color: rgba(234, 88, 12, 0.8) !important;
  border: 3px double #EA580C !important;
  color: white !important;
}

.fc-status-expired {
  background-color: rgba(107, 114, 128, 0.3) !important;
  border: 1px dotted #6B7280 !important;
  color: #6B7280 !important;
}

.fc-status-refunded {
  background-color: rgba(107, 114, 128, 0.4) !important;
  border: 1px dashed #6B7280 !important;
  color: #6B7280 !important;
}

/* === LOCKED STATE === */
.fc-event-locked {
  cursor: not-allowed !important;
  user-select: none;
}

.fc-event-locked .fc-event-resizer {
  display: none !important;
}
```

---

## 11. WIREFRAME AÇIKLAMALARI

### 11.1 Mentor Takvim Wireframe

```
┌──────────────────────────────────────────────────────────────────────────┐
│ HEADER (sticky): Logo | Mentörler | Dashboard | Randevular | Müsaitlik  │
├──────────────┬───────────────────────────────────────────┬───────────────┤
│              │  TOOLBAR                                  │               │
│  SOL PANEL   │  [◀ Bugün ▶] Şubat 2025                 │  SAĞ DRAWER   │
│  (280px)     │  [Hafta ▼] [+ Yeni Slot]                │  (360px)      │
│              │                                           │  Collapsed    │
│ ┌──────────┐ ├───────────────────────────────────────────┤ ┌───────────┐│
│ │ Haftalık  │ │         FULLCALENDAR                     │ │ (Seçilen  ││
│ │ Şablon    │ │         timeGridWeek                     │ │  slot'un  ││
│ │           │ │                                           │ │  detayı)  ││
│ │ Pzt: ☑   │ │  08 ┌─────┬─────┬─────┬─────┬─────┐     │ │           ││
│ │ 09-12     │ │     │     │     │     │     │     │     │ │ Saat:     ││
│ │ 14-18     │ │  09 │▓▓▓▓▓│▓▓▓▓▓│     │▓▓▓▓▓│▓▓▓▓▓│     │ │ 09:00-12  ││
│ │           │ │     │ Müs │ Müs │     │ Müs │ Müs │     │ │           ││
│ │ Sal: ☑   │ │  10 │▓▓▓▓▓│▓▓▓▓▓│     │▓▓▓▓▓│▓▓▓▓▓│     │ │ Durum:   ││
│ │ 09-17     │ │     │     │     │     │     │     │     │ │ Müsait   ││
│ │           │ │  11 │▓▓▓▓▓│▓▓▓▓▓│     │▓▓▓▓▓│▓▓▓▓▓│     │ │           ││
│ │ Çar: ☐   │ │     │     │     │     │     │     │     │ │ [Sil]    ││
│ │ Kapalı    │ │  12 │     │▓▓▓▓▓│     │     │     │     │ │ [Düzenle]││
│ │           │ │     │     │     │     │     │     │     │ │           ││
│ │ Per: ☑   │ │  13 │     │▓▓▓▓▓│     │     │     │     │ │ Booking: ││
│ │ 10-15     │ │     │     │     │     │     │     │     │ │ ─ Yok    ││
│ │           │ │  14 │▓▓▓▓▓│▓▓▓▓▓│     │▓▓▓▓▓│     │     │ │           ││
│ │ Cum: ☑   │ │     │ 🔒  │     │     │     │     │     │ │           ││
│ │ 09-12     │ │  15 │Ahmet│▓▓▓▓▓│     │▓▓▓▓▓│     │     │ └───────────┘│
│ │           │ │     │ Y.  │     │     │     │     │     │               │
│ │ C.t: ☐   │ │  16 │     │▓▓▓▓▓│     │     │     │     │               │
│ │ Paz: ☐   │ │     │     │     │     │     │     │     │               │
│ └──────────┘ │  17 │     │▓▓▓▓▓│     │     │     │     │               │
│              │     └─────┴─────┴─────┴─────┴─────┘     │               │
│ ┌──────────┐ │     Pzt    Sal   Çar   Per   Cum        │               │
│ │ Override  │ │                                           │               │
│ │ Takvimi   │ ├───────────────────────────────────────────┤               │
│ │ (mini)    │ │ LEGEND: ■ Müsait  ■ Rezerve 🔒  ■ Kapalı │               │
│ └──────────┘ │                                           │               │
├──────────────┴───────────────────────────────────────────┴───────────────┤
│ FOOTER                                                                   │
└──────────────────────────────────────────────────────────────────────────┘
```

**Etkileşim:**
- Sol panel: Haftalık şablon CRUD + override mini takvimi
- Ana alan: FullCalendar timeGridWeek, drag-select ile yeni slot, booked slotlarda kilit ikonu
- Sağ drawer: Tıklanan slot/booking detayı, CTA butonları
- Toolbar: View switch, tarih navigasyon, "Yeni Slot" butonu

### 11.2 Öğrenci Slot Seçim Wireframe

```
┌──────────────────────────────────────────────────────────────────────────┐
│ HEADER (sticky): Logo | Mentörler | Randevularım | Dashboard             │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌────────────────────────────┐  ┌───────────────────────────────────┐  │
│  │ 👨‍🏫 Dr. Elif Kaya          │  │                                   │  │
│  │ ⭐ 4.9 (127 değerlendirme)│  │  YKS Matematik Stratejisi         │  │
│  │ 📘 YKS Hazırlık           │  │  Süre: ○ 60dk (₺350) ○ 70dk (₺408)│  │
│  └────────────────────────────┘  │                                   │  │
│                                   └───────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │                                                                    │  │
│  │   📅 Şubat 2025                    15 Şubat Cumartesi             │  │
│  │  ┌────┬────┬────┬────┬────┬────┬────┐                             │  │
│  │  │ Pt │ Sa │ Ça │ Pe │ Cu │ Ct │ Pz │  ⏰ Müsait Saatler:        │  │
│  │  ├────┼────┼────┼────┼────┼────┼────┤                             │  │
│  │  │    │    │    │    │    │  1 │  2 │  ┌─────────────────────┐    │  │
│  │  │  3 │  4 │  5 │  6 │  7 │  8 │  9 │  │  09:00 - 10:00      │    │  │
│  │  │ 10 │ 11 │ 12 │ 13 │ 14 │•15•│ 16 │  │  60dk • ₺350        │    │  │
│  │  │ 17 │ 18 │ 19 │ 20 │ 21 │ 22 │ 23 │  └─────────────────────┘    │  │
│  │  │ 24 │ 25 │ 26 │ 27 │ 28 │    │    │  ┌─────────────────────┐    │  │
│  │  └────┴────┴────┴────┴────┴────┴────┘  │  10:00 - 11:00      │    │  │
│  │                                         │  60dk • ₺350    ✓   │    │  │
│  │  Koyu = müsait | Soluk = dolu/geçmiş   └─────────────────────┘    │  │
│  │                                         ┌─────────────────────┐    │  │
│  │  🌍 Europe/Istanbul (UTC+3) [▾]        │  14:00 - 15:00      │    │  │
│  │                                         │  60dk • ₺350        │    │  │
│  │                                         └─────────────────────┘    │  │
│  │                                                                    │  │
│  │                                         ┌─────────────────────┐    │  │
│  │                                         │     Devam Et →      │    │  │
│  │                                         │  10:00 • 60dk • ₺350│    │  │
│  │                                         └─────────────────────┘    │  │
│  │                                                                    │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                                                                          │
├──────────────────────────────────────────────────────────────────────────┤
│ FOOTER                                                                   │
└──────────────────────────────────────────────────────────────────────────┘
```

**Etkileşim:**
- Sol: dayGridMonth takvim, sadece müsait günler tıklanabilir
- Sağ: Seçilen güne ait slot listesi (buton formatında)
- Slot seçimi → "Devam Et" CTA aktif olur
- Süre seçici: Offering'in desteklediği sürelere göre radio buttons
- Fiyat orantılı güncellenir
- Timezone auto-detect + dropdown

### 11.3 Admin Master Takvim Wireframe

```
┌──────────────────────────────────────────────────────────────────────────┐
│ HEADER (sticky): Logo | Dashboard | Takvim | Anomaliler | Disputes       │
├──────────────┬───────────────────────────────────────────┬───────────────┤
│              │  TOOLBAR                                  │               │
│  SOL FİLTRE  │  [◀ Bugün ▶] 10-16 Şubat 2025           │  SAĞ DRAWER   │
│  PANELİ      │  [Hafta ▼] [Dışa Aktar]                 │  (400px)      │
│  (300px)     │                                           │               │
│              │  🔍 [Mentor/öğrenci ara...]               │               │
│ ┌──────────┐ ├───────────────────────────────────────────┤ ┌───────────┐│
│ │ MENTOR   │ │         FULLCALENDAR                     │ │ Booking   ││
│ │ FİLTRE   │ │         timeGridWeek                     │ │ #b1c2d3   ││
│ │          │ │                                           │ │           ││
│ │ ☑ Elif K.│ │  08 ┌─────┬─────┬─────┬─────┬─────┐     │ │ 👨‍🏫 Elif  ││
│ │ ☑ Ayşe D.│ │     │     │     │     │     │     │     │ │ → Ahmet   ││
│ │ ☐ Murat  │ │  09 │█████│░░░░░│     │█████│     │     │ │           ││
│ │          │ │     │Elif │Ayşe │     │Elif │     │     │ │ ⏰ 09-10  ││
│ │ STATÜ    │ │  10 │Ahmet│Zeynp│     │Mert │     │     │ │ 💰 ₺350   ││
│ │ FİLTRE   │ │     │ ✓   │ ✓   │     │ ⏳  │     │     │ │ 📍 Onaylı ││
│ │          │ │  11 │     │░░░░░│     │     │     │     │ │ 🎥 Planlı ││
│ │ ☑ Onaylı │ │     │     │     │     │     │     │     │ │           ││
│ │ ☑ Bekle. │ │  12 │     │     │     │     │     │     │ │ ────────  ││
│ │ ☑ Canlı  │ │     │     │     │     │     │     │     │ │           ││
│ │ ☐ İptal  │ │  13 │     │     │     │     │     │     │ │ [Detay]   ││
│ │ ☐ Tamam  │ │     │     │     │     │     │     │     │ │ [Override]││
│ │          │ │  14 │█████│     │░░░░░│     │     │     │ │ [İptal Et]││
│ │ ÖDEME    │ │     │Elif │     │Ayşe │     │     │     │ │ [Audit]   ││
│ │ FİLTRE   │ │  15 │Fatma│     │Can  │     │     │     │ │           ││
│ │          │ │     │ ✓   │     │ ⚠️  │     │     │     │ │ ────────  ││
│ │ ☑ Ödendi │ │  16 │     │     │░░░░░│     │     │     │ │ Audit Log ││
│ │ ☐ Bekle. │ │     │     │     │     │     │     │     │ │ • Oluştur.││
│ │ ☐ İade   │ │  17 │     │     │     │     │     │     │ │ • Ödeme   ││
│ │          │ │     └─────┴─────┴─────┴─────┴─────┘     │ │ • Onay    ││
│ │ ANOMALİ  │ │     Pzt    Sal   Çar   Per   Cum        │ │           ││
│ │          │ ├───────────────────────────────────────────┤ └───────────┘│
│ │ ⚠ 3 adet │ │ LEGEND: █ Elif ░ Ayşe ✓Onaylı ⏳Bekliyor ⚠️Dispute   │               │
│ └──────────┘ │                                           │               │
├──────────────┴───────────────────────────────────────────┴───────────────┤
│ FOOTER                                                                   │
└──────────────────────────────────────────────────────────────────────────┘
```

**Etkileşim:**
- Sol panel: Çoklu filtre (mentor multi-select, statü checkboxes, ödeme durumu, anomali sayısı)
- Ana alan: timeGridWeek, her mentor farklı renk, statü ikonu overlay
- Sağ drawer: Seçilen booking'in tam detayı + admin aksiyonları (override, iptal, audit)
- Toolbar: View switch, search, export
- Anomali badge: Sol panelde kırmızı sayaç, tıklanınca anomali listesine yönlendir

---

## EK: ÖNCELİKLENDİRME ÖNERİSİ

### Faz 1 (MVP) — 2-3 hafta
1. AvailabilityTemplate entity + CRUD API
2. Haftalık şablon UI (sol panel form)
3. Slot generation engine (şablondan otomatik slot üretimi)
4. Öğrenci slot seçim sayfası (dayGridMonth + slot listesi)
5. Booking lock model (isEditable, lockReason)
6. Temel FullCalendar entegrasyonu (timeGridWeek, eventContent)

### Faz 2 (Core) — 2-3 hafta
7. Date-specific overrides (override takvimi + modal)
8. Admin master takvim (filtre paneli + drawer)
9. Status renk kodlaması + CSS
10. Tooltip/popover standardizasyonu
11. Timezone auto-detect + selector
12. Concurrency kontrolü (SELECT FOR UPDATE, Redis lock)

### Faz 3 (Polish) — 1-2 hafta
13. Anomali panosu (ödeme-booking uyumsuzlukları)
14. Audit log UI
15. Mobil responsive (listWeek, kompakt takvim)
16. Drag & drop availability editing
17. Buffer time + min notice + booking window
18. Real-time updates (staleTime optimization, refetchOnFocus)
