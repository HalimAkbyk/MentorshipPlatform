# Session Packages (Paket Bazli Mentorluk) - Uygulama Plani

## Mevcut Durum Analizi

### Zaten var olan yapilar:
- **Offering entity**: `Title`, `Description`, `DurationMinDefault`, `PriceAmount`, `Currency`, `IsActive`, `MetadataJson`, `Type` (OneToOne/GroupClass)
- **AvailabilityTemplate**: Haftalik program, override'lar, slot uretimi
- **AvailabilitySlot**: Fiziksel zaman bloklari (StartAt, EndAt, IsBooked)
- **Booking**: OfferingId ile Offering'e bagli, StartAt/EndAt/DurationMin ile zaman dilimi
- **Order + Payment**: Iyzico entegrasyonu mevcut
- **Frontend**: Mentor dashboard, availability sayfasi, booking flow, mentor profil sayfasi

### Eksik olan seyler:
1. Mentor birden fazla paket (offering) tanimlayamiyor (onboarding'de tek seferlik)
2. Offering'de kategori/tip sistemi yok
3. Offering'e ozel aciklama/on-bilgi notu yok (sadece kisa description)
4. Ogrenci booking sirasinda slot secimi offering ile iliskilendirilmiyor (tum slotlar gosteriliyor)
5. Booking sorulari (intake questions) yok
6. Mentor profil sayfasinda paket kartlari zengin degil
7. Offering CRUD sayfasi yok (sadece dashboard'da basit listeleme)

---

## YAPILACAKLAR

### FRAZ 1: Backend - Offering Entity Zenginlestirme

#### 1.1 Offering Entity Guncelleme
**Dosya:** `Domain/Entities/Offering.cs`

Mevcut alanlara ek olarak:
```
+ Category (string, nullable)        -> Paketin kategorisi: "Matematik", "Fizik", "YKS Hazirlik" vb.
+ Subtitle (string, nullable)        -> Kisa tanitim cumlesi (max 100 karakter)
+ DetailedDescription (string, nullable) -> Zengin aciklama / on bilgi notu (uzun metin)
+ SessionType (string, nullable)     -> "PersonalizedInstruction", "ExamPrep", "HomeworkHelp", "MockInterview" vb.
+ MaxBookingDaysAhead (int, default 60) -> Bu paket icin max kac gun ilerisi rezerve edilebilir
+ MinNoticeHours (int, default 2)    -> Bu paket icin minimum randevu oncesi bildirim suresi
+ SortOrder (int, default 0)         -> Mentor profilde siralama
+ CoverImageUrl (string, nullable)   -> Paket kapak gorseli (opsiyonel)
```

> **Not:** `MetadataJson` (JSONB) zaten var. Category, SessionType gibi alanlari burada da tutabiliriz ama aranabilirlik icin ayri kolon olarak eklemek daha iyi.

#### 1.2 BookingQuestion Entity (Yeni)
**Dosya:** `Domain/Entities/BookingQuestion.cs`

Mentor'un her paket icin ogrenciye sordugu sorular:
```
BookingQuestion:
  - Id (Guid)
  - OfferingId (Guid, FK)        -> Hangi pakete ait
  - QuestionText (string, max 200) -> Soru metni
  - IsRequired (bool)             -> Zorunlu mu
  - SortOrder (int)               -> Siralama (1-4)
```

Max 4 soru per offering (Skillshare modeli).

#### 1.3 BookingQuestionResponse Entity (Yeni)
**Dosya:** `Domain/Entities/BookingQuestionResponse.cs`

Ogrencinin booking sirasinda verdigi cevaplar:
```
BookingQuestionResponse:
  - Id (Guid)
  - BookingId (Guid, FK)          -> Hangi booking'e ait
  - QuestionId (Guid, FK)         -> Hangi soruya cevap
  - AnswerText (string, max 500)  -> Ogrencinin cevabi
```

#### 1.4 Migration
- Offering tablosuna yeni kolonlar
- BookingQuestions tablosu (OfferingId FK, cascade delete)
- BookingQuestionResponses tablosu (BookingId FK, cascade delete)

---

### FRAZ 2: Backend - Offering CRUD API

#### 2.1 Offering Komutlari (Application Layer)
**Konum:** `Application/Offerings/Commands/`

| Komut | Aciklama |
|-------|----------|
| `CreateOfferingCommand` | Yeni paket olustur (title, description, duration, price, category, sessionType, detailedDescription, subtitle) |
| `UpdateOfferingCommand` | Mevcut paketi guncelle |
| `DeleteOfferingCommand` | Paketi sil (aktif booking yoksa) |
| `ToggleOfferingCommand` | Paketi aktif/pasif yap |
| `ReorderOfferingsCommand` | Paketlerin sirasini degistir |
| `UpsertBookingQuestionsCommand` | Pakete soru ekle/guncelle/sil (max 4) |

#### 2.2 Offering Sorgulari (Application Layer)
**Konum:** `Application/Offerings/Queries/`

| Sorgu | Aciklama |
|-------|----------|
| `GetMyOfferingsQuery` | Mentor'un kendi paketleri (dashboard icin) |
| `GetOfferingByIdQuery` | Tek paket detayi (sorulari dahil) |
| `GetMentorOfferingsQuery` | Bir mentor'un public paketleri (ogrenci gorunumu) |

#### 2.3 Controller
**Dosya:** `Api/Controllers/OfferingsController.cs`

```
POST   /api/offerings                    -> CreateOffering
PUT    /api/offerings/{id}               -> UpdateOffering
DELETE /api/offerings/{id}               -> DeleteOffering
PATCH  /api/offerings/{id}/toggle        -> ToggleOffering
PUT    /api/offerings/reorder            -> ReorderOfferings
PUT    /api/offerings/{id}/questions     -> UpsertBookingQuestions
GET    /api/offerings/me                 -> GetMyOfferings
GET    /api/offerings/{id}               -> GetOfferingById
GET    /api/mentors/{mentorId}/offerings -> GetMentorOfferings (public)
```

---

### FRAZ 3: Backend - Booking Flow Guncelleme

#### 3.1 CreateBookingCommand Guncelleme
Mevcut booking olusturma komutuna eklenenler:
- `QuestionResponses` listesi (opsiyonel): `[{questionId, answerText}]`
- Validation: Eger offering'de required sorular varsa, cevaplar zorunlu
- BookingQuestionResponse kayitlari olustur

#### 3.2 GetBookingByIdQuery Guncelleme
- Response'a `QuestionResponses` ekle (mentor'un gorebilmesi icin)
- Response'a `OfferingTitle`, `OfferingCategory` ekle

---

### FRAZ 4: Frontend - Mentor Paket Yonetimi Sayfasi

#### 4.1 Paket Yonetim Sayfasi (Yeni)
**Dosya:** `src/app/mentor/offerings/page.tsx`
**Rota:** `/mentor/offerings`

- Mentor'un tum paketlerini listeler (kart gorunumu)
- Her kart: Baslik, sure, fiyat, kategori, aktif/pasif badge, duzenleme/silme butonlari
- "Yeni Paket Ekle" butonu
- Drag & drop ile siralama (opsiyonel, SortOrder)
- Bos durum: "Henuz paket olusturmadiniz" mesaji

#### 4.2 Paket Olusturma/Duzenleme Modali veya Sayfasi
**Dosya:** `src/app/mentor/offerings/[id]/page.tsx` veya modal component

Form alanlari:
```
- Paket Adi (title)*              -> "Matematik Birebir Mentorluk"
- Kisa Tanitim (subtitle)         -> "TYT/AYT matematik icin kisisel calisma plani"
- Kategori (category)*            -> Dropdown: Matematik, Fizik, Kimya, Biyoloji, Turkce, YKS Hazirlik, vs.
- Oturum Tipi (sessionType)       -> Dropdown: Birebir Ders, Sinav Hazirlik, Odev Yardimi, vs.
- Detayli Aciklama (detailedDescription) -> Textarea (zengin metin)
- Sure (durationMinDefault)*      -> 30dk, 45dk, 60dk, 90dk, 120dk secenekleri
- Ucret (priceAmount)*            -> Sayi girisi
- Para Birimi (currency)          -> TRY (varsayilan)
- Aktif Mi (isActive)             -> Toggle
```

#### 4.3 Booking Sorulari Yonetimi
Paket duzenleme sayfasinda alt bolum:
- Max 4 soru ekleme
- Her soru icin: soru metni + zorunlu mu toggle
- Varsayilan soru onerisi: "Bu oturum hakkinda beklentileriniz nelerdir?"

#### 4.4 Mentor Dashboard Guncelleme
- "Hizmetlerim" kartinda "Paketleri Yonet" butonu ekle
- Sidebar'a "Paketlerim" linki ekle

---

### FRAZ 5: Frontend - Ogrenci Tarafindaki Paket Gorunumu

#### 5.1 Mentor Profil Sayfasi Guncelleme
**Dosya:** `src/app/public/mentors/[id]/page.tsx`

Hizmetler (Services) sekmesi yeniden tasarimi:
- Her paket icin zengin kart:
  ```
  +------------------------------------------+
  | [Kategori Badge]        [Sure] | [Fiyat] |
  | Paket Adi (baslik)                       |
  | Kisa Tanitim (subtitle)                  |
  | Detayli Aciklama (kisaltilmis + devamini |
  | oku linki)                               |
  |                                          |
  | [Randevu Al ->]                          |
  +------------------------------------------+
  ```
- Paketler SortOrder'a gore siralanir
- Sadece aktif paketler gosterilir

#### 5.2 Booking Sayfasi Guncelleme
**Dosya:** `src/app/student/bookings/new/content.tsx`

Mevcut 3 adimli flow'a ekleme:
```
Adim 1: Tarih Sec (mevcut - degisiklik yok)
Adim 2: Saat Sec (mevcut - degisiklik yok)
Adim 3: Sorulari Cevapla (YENI - eger offering'de soru varsa)
Adim 4: Onayla ve Ode (mevcut - soru cevaplari da gonderilir)
```

- Adim 3 sadece offering'de soru tanimlanmissa gosterilir
- Required sorular icin validation
- Ogrenci cevaplarini girer, "Devam Et" ile odeme adimina gecer

---

### FRAZ 6: Frontend - Mentor Tarafinda Booking Detay Guncelleme

#### 6.1 Booking Detay Sayfasi
**Dosya:** `src/app/mentor/bookings/[id]/page.tsx`

Mevcut sayfaya ekleme:
- "Ogrenci Notlari" bolumu: Booking sorulari ve ogrencinin cevaplari
- Offering bilgileri: Paket adi, kategori, sure, ucret

---

## TEKNIK NOTLAR

### Veri Tabani Sema Degisiklikleri
```sql
-- Offering tablosuna yeni kolonlar
ALTER TABLE "Offerings" ADD COLUMN "Category" text;
ALTER TABLE "Offerings" ADD COLUMN "Subtitle" varchar(100);
ALTER TABLE "Offerings" ADD COLUMN "DetailedDescription" text;
ALTER TABLE "Offerings" ADD COLUMN "SessionType" varchar(50);
ALTER TABLE "Offerings" ADD COLUMN "MaxBookingDaysAhead" int NOT NULL DEFAULT 60;
ALTER TABLE "Offerings" ADD COLUMN "MinNoticeHours" int NOT NULL DEFAULT 2;
ALTER TABLE "Offerings" ADD COLUMN "SortOrder" int NOT NULL DEFAULT 0;
ALTER TABLE "Offerings" ADD COLUMN "CoverImageUrl" text;

-- Yeni tablo: BookingQuestions
CREATE TABLE "BookingQuestions" (
    "Id" uuid PRIMARY KEY,
    "OfferingId" uuid NOT NULL REFERENCES "Offerings"("Id") ON DELETE CASCADE,
    "QuestionText" varchar(200) NOT NULL,
    "IsRequired" boolean NOT NULL DEFAULT false,
    "SortOrder" int NOT NULL DEFAULT 0
);

-- Yeni tablo: BookingQuestionResponses
CREATE TABLE "BookingQuestionResponses" (
    "Id" uuid PRIMARY KEY,
    "BookingId" uuid NOT NULL REFERENCES "Bookings"("Id") ON DELETE CASCADE,
    "QuestionId" uuid NOT NULL REFERENCES "BookingQuestions"("Id") ON DELETE RESTRICT,
    "AnswerText" varchar(500) NOT NULL
);
```

### Mevcut Yapilarla Uyumluluk
- **AvailabilityTemplate/Slot sistemi degismiyor** - Mentor ayni sekilde haftalik programini ayarlar
- **Booking entity degismiyor** - Sadece QuestionResponses iliskisi eklenir
- **Order/Payment sistemi degismiyor** - Ayni Iyzico akisi devam eder
- **Offering entity genisletilir** - Mevcut `upsertMyOneToOneOffering` yerine tam CRUD
- **MentorsController'daki mevcut offering endpointleri korunur**, yeni OfferingsController eklenir

### Varsayilan Kategoriler (Enum degil, serbest metin + oneri listesi)
```
Matematik, Fizik, Kimya, Biyoloji, Turkce, Edebiyat, Tarih, Cografya,
Felsefe, Ingilizce, Almanca, YKS Genel, TYT Hazirlik, AYT Hazirlik,
DGS Hazirlik, ALES Hazirlik, Universite Danismanligi, Kariyer Mentorlu
```

### Varsayilan Oturum Tipleri
```
Birebir Ders, Sinav Hazirlik, Odev Yardimi, Konu Anlatimi,
Soru Cozumu, Deneme Sinavi Analizi, Universite Danismanligi,
Motivasyon Destegi
```

---

## UYGULAMA SIRASI (Onerilen)

| Sira | Is | Tahmini Dosya Sayisi |
|------|----|---------------------|
| 1 | Backend: Offering entity guncelleme + migration | 3-4 dosya |
| 2 | Backend: BookingQuestion + BookingQuestionResponse entity + migration | 4-5 dosya |
| 3 | Backend: EF Core configurations (yeni tablolar) | 2-3 dosya |
| 4 | Backend: Offering CRUD komutlari + validatorler | 10-12 dosya |
| 5 | Backend: Offering sorgulari | 4-6 dosya |
| 6 | Backend: OfferingsController | 1 dosya |
| 7 | Backend: CreateBooking guncelleme (question responses) | 2-3 dosya |
| 8 | Backend: GetBookingById guncelleme | 1-2 dosya |
| 9 | Frontend: API client + types + hooks (offerings) | 3-4 dosya |
| 10 | Frontend: Mentor paket yonetim sayfasi | 2-3 dosya |
| 11 | Frontend: Mentor profil sayfasi paket kartlari | 1 dosya |
| 12 | Frontend: Booking flow soru adimi | 1-2 dosya |
| 13 | Frontend: Booking detay soru gosterimi | 1 dosya |
| 14 | Docker build + deploy + test | - |

**Toplam: ~35-45 dosya degisikligi/ekleme**

---

## KAPSAM DISI (Bu plana dahil olmayan)

- Indirim kodu sistemi (discount codes)
- Google Calendar entegrasyonu
- Otomatik/manuel randevu onaylama modu
- Mentor tanitim videosu
- Platform komisyon hesaplama
- Farkli para birimleri arasi donusum
- Paket bazli availability (her paket icin ayri takvim) - tum paketler ayni takvimi kullanir
