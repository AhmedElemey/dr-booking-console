using Console.Api.Domain;

namespace Console.Api.Features.Catalog;

public sealed record FeatureDefinition(
    string Key,
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn,
    string Group,
    bool Available,
    bool DefaultEnabled,
    RepoKind? CreatesRepo);

public static class FeatureCatalog
{
    public static readonly IReadOnlyList<FeatureDefinition> All =
    [
        new("mobileApp", "تطبيق الموبايل", "Mobile app", "تطبيق للمرضى على أندرويد وآيفون.", "A patient app for Android and iOS.", "Channels", true, true, RepoKind.Mobile),
        new("website", "الموقع الإلكتروني", "Website", "موقع للحجز عبر الإنترنت.", "A public website with online booking.", "Channels", true, true, RepoKind.Website),
        new("guestBrowsing", "التصفح كزائر", "Guest browsing", "السماح بتصفح الأطباء والمواعيد قبل تسجيل الدخول.", "Let visitors browse doctors and slots before signing in.", "Booking", true, true, null),
        new("patientSelfService", "تعديل الحجز بواسطة المريض", "Patient self-service", "يستطيع المريض إلغاء الحجز أو تغيير موعده بنفسه.", "Patients can cancel or reschedule their own bookings.", "Booking", true, true, null),
        new("pushReminders", "تذكيرات الإشعارات", "Push reminders", "إرسال إشعارات التأكيد والتذكير بالمواعيد.", "Send booking confirmations and appointment reminders as push notifications.", "Notifications", true, true, null),
        new("smsNotifications", "رسائل SMS", "SMS notifications", "إرسال الإشعارات عبر رسائل SMS.", "Send notifications by SMS.", "Notifications", true, false, null),
        new("englishLanguage", "اللغة الإنجليزية", "English language", "إتاحة الواجهة بالإنجليزية بجانب العربية.", "Offer the English interface alongside Arabic.", "Language", true, true, null),
        new("multiBranch", "تعدد الفروع", "Multiple branches", "إدارة أكثر من فرع للعيادة.", "Manage more than one clinic branch.", "Organisation", true, true, null),
        new("onlinePayments", "الدفع الإلكتروني", "Online payments", "دفع رسوم الكشف عبر الإنترنت.", "Collect consultation fees online.", "Payments", false, false, null),
        new("whatsappReminders", "تذكيرات واتساب", "WhatsApp reminders", "إرسال التذكيرات عبر واتساب.", "Send reminders over WhatsApp.", "Notifications", false, false, null),
        new("waitingList", "قائمة الانتظار", "Waiting list", "تسجيل المرضى في قائمة انتظار للمواعيد الممتلئة.", "Let patients join a waiting list for full slots.", "Booking", false, false, null),
        new("holidayCalendar", "تقويم الإجازات", "Holiday calendar", "إغلاق العيادة في الإجازات الرسمية تلقائيا.", "Close the clinic automatically on public holidays.", "Organisation", false, false, null),
        new("workingHoursTemplates", "قوالب ساعات العمل", "Working hours templates", "قوالب جاهزة لجداول عمل الأطباء.", "Reusable working-hours templates for doctors.", "Organisation", false, false, null),
        new("patientRecords", "ملفات المرضى", "Patient records", "سجل طبي لكل مريض.", "A medical record for every patient.", "Records", false, false, null),
        new("reviews", "تقييمات المرضى", "Reviews", "تقييمات وآراء المرضى عن الأطباء.", "Patient ratings and reviews for doctors.", "Growth", false, false, null),
        new("reports", "التقارير", "Reports", "تقارير الحجوزات والإيرادات.", "Booking and revenue reports.", "Insight", false, false, null),
        new("promotions", "العروض والخصومات", "Promotions", "عروض وكوبونات خصم.", "Offers and discount coupons.", "Growth", false, false, null),
        new("recallCampaigns", "حملات الاستدعاء", "Recall campaigns", "تذكير المرضى بالزيارات الدورية.", "Remind patients about routine visits.", "Growth", false, false, null),
        new("videoConsultation", "الاستشارة بالفيديو", "Video consultation", "استشارات طبية عن بعد.", "Remote video consultations.", "Channels", false, false, null),
        new("familyAccounts", "حسابات العائلة", "Family accounts", "حجز مواعيد لأفراد العائلة من حساب واحد.", "Book for family members from one account.", "Booking", false, false, null),
        new("insurance", "التأمين الطبي", "Insurance", "قبول شركات التأمين الطبي.", "Accept insurance providers.", "Payments", false, false, null),
    ];

    public static FeatureDefinition? Find(string key) => All.FirstOrDefault(f => f.Key == key);

    public static IEnumerable<FeatureDefinition> Available => All.Where(f => f.Available);
}
