using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Api;

public static class EmailTemplateSeedData
{
    public static async Task SeedEmailTemplates(ApplicationDbContext db)
    {
        if (await db.NotificationTemplates.AnyAsync())
            return;

        var templates = new List<NotificationTemplate>
        {
            // 1. welcome
            NotificationTemplate.Create(
                "welcome",
                "Hoşgeldin E-postası",
                "MentorHub'a Hoş Geldin! \ud83c\udf93",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Hoş Geldin, {{displayName}}! \ud83c\udf89</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        MentorHub ailesine katıldığın için çok mutluyuz. Platformumuzda deneyimli mentörlerle birebir görüşmeler yapabilir,
                        grup derslerine katılabilir ve kariyerine yön verebilirsin.
                    </p>
                    <h2 style=""margin:0 0 12px;font-size:18px;font-weight:600;color:#111827;"">İlk Adımlar</h2>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom:24px;"">
                        <tr><td style=""padding:8px 0;font-size:15px;color:#374151;"">✅ Profilini tamamla — fotoğraf ve biyografi ekle</td></tr>
                        <tr><td style=""padding:8px 0;font-size:15px;color:#374151;"">✅ İlgi alanlarına göre mentör ara</td></tr>
                        <tr><td style=""padding:8px 0;font-size:15px;color:#374151;"">✅ İlk dersini rezerve et</td></tr>
                        <tr><td style=""padding:8px 0;font-size:15px;color:#374151;"">✅ Grup derslerini keşfet</td></tr>
                    </table>
                    <div style=""text-align:center;margin:32px 0;"">
                        <a href=""{{platformUrl}}/public/mentors"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;"">Mentörleri Keşfet</a>
                    </div>
                "),
                @"[""displayName"",""platformUrl""]",
                "Email"
            ),

            // 2. booking_confirmed
            NotificationTemplate.Create(
                "booking_confirmed",
                "Booking Onayı (Öğrenci)",
                "Rezervasyonunuz Onaylandı! \ud83c\udf89",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Rezervasyon Onaylandı \u2705</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Dersiniz başarıyla oluşturuldu. Aşağıda ders detaylarınız yer almaktadır.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Mentör</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{mentorName}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Ders</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{offeringTitle}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Tarih</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{bookingDate}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Saat</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{bookingTime}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Ders saatinden önce hatırlatma e-postası alacaksınız. Herhangi bir sorunuz varsa mentörünüze mesaj gönderebilirsiniz.
                    </p>
                "),
                @"[""mentorName"",""bookingDate"",""bookingTime"",""offeringTitle""]",
                "Email"
            ),

            // 3. booking_confirmed_mentor
            NotificationTemplate.Create(
                "booking_confirmed_mentor",
                "Booking Bildirimi (Mentör)",
                "Yeni Ders Rezervasyonu \ud83d\udcc5",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Yeni Ders Rezervasyonu \ud83d\udcc5</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Yeni bir ders rezervasyonunuz var! Detaylar aşağıdadır.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Öğrenci</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{studentName}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Ders</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{offeringTitle}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Tarih</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{bookingDate}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Saat</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{bookingTime}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Lütfen belirtilen saatte hazır olun. Herhangi bir değişiklik gerekiyorsa öğrencinizle iletişime geçin.
                    </p>
                "),
                @"[""studentName"",""bookingDate"",""bookingTime"",""offeringTitle""]",
                "Email"
            ),

            // 4. booking_reminder
            NotificationTemplate.Create(
                "booking_reminder",
                "Ders Hatırlatma",
                "\u23f0 Dersiniz {{timeframe}} sonra başlıyor!",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Ders Hatırlatması \u23f0</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{otherPartyName}}</strong> ile <strong>{{offeringTitle}}</strong> dersiniz <strong>{{timeframe}}</strong> sonra başlıyor.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Tarih</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{bookingDate}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Saat</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{bookingTime}}</td></tr>
                    </table>
                    <div style=""text-align:center;margin:32px 0;"">
                        <a href=""{{classroomUrl}}"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;"">Derse Katıl</a>
                    </div>
                "),
                @"[""otherPartyName"",""bookingDate"",""bookingTime"",""timeframe"",""classroomUrl"",""offeringTitle""]",
                "Email"
            ),

            // 5. booking_cancelled_student
            NotificationTemplate.Create(
                "booking_cancelled_student",
                "İptal Bildirimi (Öğrenci)",
                "Rezervasyon İptal Edildi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Rezervasyon İptal Edildi</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{mentorName}}</strong> ile <strong>{{offeringTitle}}</strong> dersiniz iptal edilmiştir.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#fef2f2;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#991b1b;font-weight:600;"">İptal Nedeni</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{reason}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        İade koşulları iptal zamanlamasına göre uygulanacaktır. Ödemeniz varsa iade süreci otomatik olarak başlatılacaktır.
                        Yeni bir ders rezervasyonu yapmak için platformu ziyaret edebilirsiniz.
                    </p>
                "),
                @"[""mentorName"",""reason"",""offeringTitle""]",
                "Email"
            ),

            // 6. booking_cancelled_mentor
            NotificationTemplate.Create(
                "booking_cancelled_mentor",
                "İptal Bildirimi (Mentör)",
                "Ders İptali Bildirimi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Ders İptali Bildirimi</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{studentName}}</strong> ile <strong>{{offeringTitle}}</strong> dersiniz iptal edilmiştir.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#fef2f2;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#991b1b;font-weight:600;"">İptal Nedeni</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{reason}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        İlgili zaman dilimi tekrar müsait hale getirilmiştir. Takviminizdeki güncellemeleri kontrol edebilirsiniz.
                    </p>
                "),
                @"[""studentName"",""reason"",""offeringTitle""]",
                "Email"
            ),

            // 7. booking_completed
            NotificationTemplate.Create(
                "booking_completed",
                "Ders Tamamlandı",
                "Dersiniz Tamamlandı \u2705",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Ders Tamamlandı \u2705</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{otherPartyName}}</strong> ile <strong>{{offeringTitle}}</strong> dersiniz (<strong>{{bookingDate}}</strong>) başarıyla tamamlandı.
                    </p>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Deneyiminizi değerlendirmek diğer kullanıcılara yardımcı olacaktır. Lütfen birkaç dakikanızı ayırarak bir değerlendirme bırakın.
                    </p>
                    <div style=""text-align:center;margin:32px 0;"">
                        <a href=""{{platformUrl}}/student/bookings"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;"">Değerlendirme Yap</a>
                    </div>
                "),
                @"[""otherPartyName"",""bookingDate"",""offeringTitle"",""platformUrl""]",
                "Email"
            ),

            // 8. reschedule_request
            NotificationTemplate.Create(
                "reschedule_request",
                "Saat Değişikliği Talebi",
                "Saat Değişikliği Talebi \ud83d\udd04",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Saat Değişikliği Talebi \ud83d\udd04</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{otherPartyName}}</strong>, <strong>{{offeringTitle}}</strong> dersi için saat değişikliği talep etti.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Mevcut Tarih</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{oldDate}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Yeni Tarih</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#16a34a;"">{{newDate}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Lütfen bu talebi onaylayın veya reddedin. Talebinize platformdan yanıt verebilirsiniz.
                    </p>
                "),
                @"[""otherPartyName"",""oldDate"",""newDate"",""offeringTitle""]",
                "Email"
            ),

            // 9. reschedule_approved
            NotificationTemplate.Create(
                "reschedule_approved",
                "Saat Değişikliği Onay",
                "Saat Değişikliği Onaylandı \u2705",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Saat Değişikliği Onaylandı \u2705</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{otherPartyName}}</strong>, <strong>{{offeringTitle}}</strong> dersi için saat değişikliği talebinizi onayladı.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f0fdf4;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#166534;font-weight:600;"">Yeni Ders Tarihi</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:18px;font-weight:700;color:#111827;"">{{newDate}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Lütfen yeni tarihi takviminize ekleyin. Ders saatinden önce hatırlatma alacaksınız.
                    </p>
                "),
                @"[""otherPartyName"",""newDate"",""offeringTitle""]",
                "Email"
            ),

            // 10. reschedule_rejected
            NotificationTemplate.Create(
                "reschedule_rejected",
                "Saat Değişikliği Red",
                "Saat Değişikliği Reddedildi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Saat Değişikliği Reddedildi</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{otherPartyName}}</strong>, saat değişikliği talebinizi reddetti. Ders orijinal tarih ve saatinde gerçekleşecektir.
                    </p>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Farklı bir zamana ihtiyacınız varsa yeni bir talep oluşturabilir veya mentörünüzle mesaj yoluyla iletişime geçebilirsiniz.
                    </p>
                "),
                @"[""otherPartyName""]",
                "Email"
            ),

            // 11. group_class_enrolled
            NotificationTemplate.Create(
                "group_class_enrolled",
                "Grup Dersine Kayıt",
                "Grup Dersine Kaydınız Onaylandı! \ud83c\udf89",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Grup Dersine Kayıt Onaylandı \ud83c\udf89</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Grup dersine başarıyla kaydoldunuz! İşte ders detayları:
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Ders</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{className}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Mentör</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{mentorName}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Tarih</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{classDate}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Saat</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{classTime}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Ders saatinde ""Grup Derslerim"" sayfasından derse katılabilirsiniz.
                    </p>
                "),
                @"[""className"",""mentorName"",""classDate"",""classTime""]",
                "Email"
            ),

            // 12. group_class_cancelled
            NotificationTemplate.Create(
                "group_class_cancelled",
                "Grup Dersi İptal",
                "Grup Dersi İptal Edildi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Grup Dersi İptal Edildi</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{mentorName}}</strong> tarafından oluşturulan <strong>{{className}}</strong> grup dersi iptal edilmiştir.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#fef2f2;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#991b1b;font-weight:600;"">İptal Nedeni</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{reason}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Ödemeniz varsa iade işlemi otomatik olarak başlatılacaktır. Diğer grup derslerini keşfetmek için platformu ziyaret edebilirsiniz.
                    </p>
                "),
                @"[""className"",""mentorName"",""reason""]",
                "Email"
            ),

            // 13. group_class_completed
            NotificationTemplate.Create(
                "group_class_completed",
                "Grup Dersi Tamamlandı",
                "Grup Dersi Tamamlandı \u2705",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Grup Dersi Tamamlandı \u2705</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{mentorName}}</strong> tarafından verilen <strong>{{className}}</strong> grup dersi başarıyla tamamlandı.
                    </p>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Deneyiminizi değerlendirmek diğer öğrencilere yardımcı olacaktır. Lütfen dersi değerlendirmeyi unutmayın.
                    </p>
                "),
                @"[""className"",""mentorName""]",
                "Email"
            ),

            // 14. course_enrolled
            NotificationTemplate.Create(
                "course_enrolled",
                "Kursa Kayıt",
                "Kursa Kaydınız Onaylandı! \ud83c\udf93",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Kursa Kayıt Onaylandı \ud83c\udf93</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{mentorName}}</strong> tarafından oluşturulan <strong>{{courseTitle}}</strong> kursuna başarıyla kaydoldunuz.
                    </p>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Artık kurs içeriklerine erişebilir ve kendi hızınızda öğrenmeye başlayabilirsiniz.
                    </p>
                    <div style=""text-align:center;margin:32px 0;"">
                        <a href=""{{platformUrl}}/student/my-courses"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;"">Kurslarıma Git</a>
                    </div>
                "),
                @"[""courseTitle"",""mentorName"",""platformUrl""]",
                "Email"
            ),

            // 15. course_review_result
            NotificationTemplate.Create(
                "course_review_result",
                "Kurs İnceleme Sonucu",
                "Kursunuz İncelendi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Kurs İnceleme Sonucu</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{courseTitle}}</strong> kursunuz admin ekibi tarafından incelendi.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Sonuç</td><td style=""padding:8px 16px;font-size:15px;font-weight:700;color:#111827;"">{{outcome}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;"">Admin Notları</td><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{adminNotes}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Detaylı bilgi için kurs yönetimi sayfanızı kontrol edebilirsiniz.
                    </p>
                "),
                @"[""courseTitle"",""outcome"",""adminNotes""]",
                "Email"
            ),

            // 16. new_review
            NotificationTemplate.Create(
                "new_review",
                "Yeni Değerlendirme",
                "Yeni Bir Değerlendirme Aldınız \u2b50",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Yeni Değerlendirme \u2b50</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{studentName}}</strong>, <strong>{{offeringTitle}}</strong> dersiniz için bir değerlendirme bıraktı.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#fffbeb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#92400e;font-weight:600;"">Puan</td><td style=""padding:8px 16px;font-size:18px;font-weight:700;color:#111827;"">{{rating}} / 5 \u2b50</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#92400e;font-weight:600;"">Yorum</td><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{comment}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Değerlendirmeler profilinizde görüntülenir ve yeni öğrencilerin size ulaşmasına yardımcı olur.
                    </p>
                "),
                @"[""studentName"",""rating"",""comment"",""offeringTitle""]",
                "Email"
            ),

            // 17. verification_approved
            NotificationTemplate.Create(
                "verification_approved",
                "Doğrulama Onay",
                "\u2705 Doğrulamanız Onaylandı!",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Doğrulama Onaylandı \u2705</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{verificationType}}</strong> doğrulamanız başarıyla onaylandı. Artık platform üzerinde tam erişime sahipsiniz.
                    </p>
                    <div style=""background-color:#f0fdf4;border-radius:8px;padding:20px;margin-bottom:24px;text-align:center;"">
                        <p style=""margin:0;font-size:32px;"">&#x1f389;</p>
                        <p style=""margin:8px 0 0;font-size:16px;font-weight:600;color:#166534;"">Tebrikler!</p>
                    </div>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Profilinizi tamamlayarak ders vermeye başlayabilirsiniz.
                    </p>
                "),
                @"[""verificationType""]",
                "Email"
            ),

            // 18. verification_rejected
            NotificationTemplate.Create(
                "verification_rejected",
                "Doğrulama Red",
                "Doğrulama Talebi Reddedildi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Doğrulama Reddedildi</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{verificationType}}</strong> doğrulama talebiniz reddedildi.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#fef2f2;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#991b1b;font-weight:600;"">Red Nedeni</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{reason}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Belirtilen eksiklikleri giderdikten sonra yeniden başvurabilirsiniz. Yardıma ihtiyacınız varsa destek ekibiyle iletişime geçebilirsiniz.
                    </p>
                "),
                @"[""verificationType"",""reason""]",
                "Email"
            ),

            // 19. mentor_published
            NotificationTemplate.Create(
                "mentor_published",
                "Mentör Yayınlandı",
                "Profiliniz Yayında! \ud83c\udf89",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Profiliniz Yayında! \ud83c\udf89</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Tebrikler <strong>{{mentorName}}</strong>! Mentör profiliniz artık yayında ve öğrenciler sizi bulabilir.
                    </p>
                    <div style=""background-color:#eff6ff;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <h2 style=""margin:0 0 12px;font-size:16px;font-weight:600;color:#1e40af;"">Başarılı bir mentör olmak için ipuçları:</h2>
                        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"">
                            <tr><td style=""padding:6px 0;font-size:14px;color:#374151;"">&#x2022; Profilinizi detaylı ve güncel tutun</td></tr>
                            <tr><td style=""padding:6px 0;font-size:14px;color:#374151;"">&#x2022; Müsaitlik takvimini düzenli olarak güncelleyin</td></tr>
                            <tr><td style=""padding:6px 0;font-size:14px;color:#374151;"">&#x2022; Öğrencilere hızlı yanıt verin</td></tr>
                            <tr><td style=""padding:6px 0;font-size:14px;color:#374151;"">&#x2022; Grup dersleri oluşturarak daha fazla öğrenciye ulaşın</td></tr>
                        </table>
                    </div>
                    <div style=""text-align:center;margin:32px 0;"">
                        <a href=""{{platformUrl}}/mentor/dashboard"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;"">Panelime Git</a>
                    </div>
                "),
                @"[""mentorName"",""platformUrl""]",
                "Email"
            ),

            // 20. unread_messages
            NotificationTemplate.Create(
                "unread_messages",
                "Okunmamış Mesaj",
                "\ud83d\udcac {{senderName}} size mesaj gönderdi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Okunmamış Mesajlarınız Var \ud83d\udcac</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{senderName}}</strong> size <strong>{{offeringTitle}}</strong> ile ilgili mesaj gönderdi.
                        <strong>{{unreadCount}}</strong> okunmamış mesajınız bulunmaktadır.
                    </p>
                    <div style=""text-align:center;margin:32px 0;"">
                        <a href=""{{messagesUrl}}"" style=""display:inline-block;padding:14px 32px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;font-size:16px;font-weight:600;"">Mesajları Görüntüle</a>
                    </div>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Mesajlarınızı zamanında okumak, etkili bir iletişim sağlamak açısından önemlidir.
                    </p>
                "),
                @"[""senderName"",""offeringTitle"",""unreadCount"",""messagesUrl""]",
                "Email"
            ),

            // 21. payout_processed
            NotificationTemplate.Create(
                "payout_processed",
                "Ödeme Yapıldı",
                "Ödemeniz İşlendi \ud83d\udcb0",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">Ödeme İşlendi \ud83d\udcb0</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        Ödemeniz başarıyla işlendi ve hesabınıza aktarılmıştır.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f0fdf4;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#166534;font-weight:600;"">Tutar</td><td style=""padding:8px 16px;font-size:20px;font-weight:700;color:#166534;"">{{amount}}</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#166534;font-weight:600;"">İşlem Tarihi</td><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{paymentDate}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Ödeme detaylarını panelinizdeki ""Kazançlarım"" bölümünden görüntüleyebilirsiniz.
                    </p>
                "),
                @"[""amount"",""paymentDate""]",
                "Email"
            ),

            // 22. dispute_opened
            NotificationTemplate.Create(
                "dispute_opened",
                "İtiraz Açıldı",
                "Ders İtirazı Bildirimi",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">İtiraz Bildirimi</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{otherPartyName}}</strong> tarafından <strong>{{bookingDate}}</strong> tarihli ders için bir itiraz açılmıştır.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#fff7ed;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#9a3412;font-weight:600;"">İtiraz Nedeni</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:15px;color:#374151;"">{{reason}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        İtiraz admin ekibi tarafından incelenecektir. Sonuç hakkında ayrıca bilgilendirileceksiniz.
                    </p>
                "),
                @"[""bookingDate"",""reason"",""otherPartyName""]",
                "Email"
            ),

            // 23. dispute_resolved
            NotificationTemplate.Create(
                "dispute_resolved",
                "İtiraz Çözüldü",
                "İtiraz Sonuçlandı",
                WrapInLayout(@"
                    <h1 style=""margin:0 0 16px;font-size:24px;font-weight:700;color:#111827;"">İtiraz Sonuçlandı</h1>
                    <p style=""margin:0 0 24px;font-size:16px;color:#374151;line-height:1.6;"">
                        <strong>{{bookingDate}}</strong> tarihli ders için açılan itiraz incelendi ve sonuçlandı.
                    </p>
                    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
                        <tr><td style=""padding:8px 16px;font-size:14px;color:#6b7280;font-weight:600;"">Karar</td></tr>
                        <tr><td style=""padding:8px 16px;font-size:15px;font-weight:600;color:#111827;"">{{resolution}}</td></tr>
                    </table>
                    <p style=""margin:0;font-size:14px;color:#6b7280;line-height:1.6;"">
                        Bu karar hakkında sorularınız varsa destek ekibimizle iletişime geçebilirsiniz.
                    </p>
                "),
                @"[""bookingDate"",""resolution""]",
                "Email"
            ),
        };

        db.NotificationTemplates.AddRange(templates);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Wraps the given inner HTML content in a shared, mobile-responsive email layout.
    /// </summary>
    private static string WrapInLayout(string content)
    {
        return $@"<!DOCTYPE html>
<html lang=""tr"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
    <title>{{{{platformName}}}}</title>
    <!--[if mso]>
    <style type=""text/css"">
        table, td {{{{ font-family: Arial, sans-serif; }}}}
    </style>
    <![endif]-->
</head>
<body style=""margin:0;padding:0;background-color:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f3f4f6;"">
        <tr>
            <td align=""center"" style=""padding:32px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;"">
                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#2563eb;border-radius:12px 12px 0 0;padding:24px 32px;text-align:center;"">
                            <h1 style=""margin:0;font-size:22px;font-weight:700;color:#ffffff;letter-spacing:0.5px;"">{{{{platformName}}}}</h1>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style=""background-color:#ffffff;padding:32px;"">
                            {content.Trim()}
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#f9fafb;border-radius:0 0 12px 12px;padding:24px 32px;text-align:center;border-top:1px solid #e5e7eb;"">
                            <p style=""margin:0 0 8px;font-size:13px;color:#9ca3af;"">Bu otomatik bir mesajdır, lütfen yanıtlamayın.</p>
                            <p style=""margin:0;font-size:12px;color:#d1d5db;"">&copy; {{{{year}}}} {{{{platformName}}}}. Tüm hakları saklıdır.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
