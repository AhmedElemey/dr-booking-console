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
    RepoKind? CreatesRepo,
    string? SetupNoteAr = null,
    string? SetupNoteEn = null);

public static class FeatureCatalog
{
    private static readonly List<FeatureDefinition> Entries =
    [
        new("mobileApp", "تطبيق الموبايل", "Mobile app", "تطبيق للمرضى على أندرويد وآيفون.", "A patient app for Android and iOS.", "Channels", true, true, RepoKind.Mobile),
        new("website", "الموقع الإلكتروني", "Website", "موقع للحجز عبر الإنترنت.", "A public website with online booking.", "Channels", true, true, RepoKind.Website),
        new("guestBrowsing", "التصفح كزائر", "Guest browsing", "السماح بتصفح الأطباء والمواعيد قبل تسجيل الدخول.", "Let visitors browse doctors and slots before signing in.", "Booking", true, true, null),
        new("patientSelfService", "تعديل الحجز بواسطة المريض", "Patient self-service", "يستطيع المريض إلغاء الحجز أو تغيير موعده بنفسه.", "Patients can cancel or reschedule their own bookings.", "Booking", true, true, null),
        new("pushReminders", "تذكيرات الإشعارات", "Push reminders", "إرسال إشعارات التأكيد والتذكير بالمواعيد.", "Send booking confirmations and appointment reminders as push notifications.", "Notifications", true, true, null),
        new("smsNotifications", "رسائل SMS", "SMS notifications", "إرسال الإشعارات عبر رسائل SMS.", "Send notifications by SMS.", "Notifications", true, false, null),
        new("englishLanguage", "اللغة الإنجليزية", "English language", "إتاحة الواجهة بالإنجليزية بجانب العربية.", "Offer the English interface alongside Arabic.", "Language", true, true, null),
        new("multiBranch", "تعدد الفروع", "Multiple branches", "إدارة أكثر من فرع للعيادة.", "Manage more than one clinic branch.", "Organisation", true, true, null),
        new("onlinePayments", "الدفع الإلكتروني", "Online payments", "دفع رسوم الكشف عبر الإنترنت.", "Collect consultation fees online.", "Payments", true, false, null,
            "يتطلب إعداد مزود الدفع في الخادم: Payments:Provider=Paymob مع مفاتيح Paymob. بدونها تعمل صفحة دفع تجريبية.",
            "Needs setup in the clinic backend: Payments:Provider=Paymob plus the Paymob keys. Without them a mock checkout is used."),
        new("whatsappReminders", "تذكيرات واتساب", "WhatsApp reminders", "إرسال التذكيرات عبر واتساب.", "Send reminders over WhatsApp.", "Notifications", true, false, null,
            "يتطلب إعدادا في الخادم: WhatsApp:Provider=Meta مع مفاتيح واتساب. بدونها تُسجَّل الرسائل فقط دون إرسال.",
            "Needs setup in the clinic backend: WhatsApp:Provider=Meta plus the WhatsApp keys. Without them messages are only logged, not sent."),
        new("waitingList", "قائمة الانتظار", "Waiting list", "تسجيل المرضى في قائمة انتظار للمواعيد الممتلئة.", "Let patients join a waiting list for full slots.", "Booking", true, false, null),
        new("liveQueue", "قائمة الانتظار الحية", "Live queue", "متابعة وصول المرضى وترتيبهم في العيادة لحظة بلحظة.", "Track patient arrivals and the in-clinic queue in real time.", "Booking", true, false, null),
        new("manualReview", "تأكيد الحجز يدويا", "Manual booking review", "تبقى الحجوزات بانتظار تأكيد العيادة قبل أن تصبح مؤكدة.", "Bookings wait for the clinic to confirm them before they are final.", "Booking", true, false, null),
        new("publicPrices", "إظهار الأسعار للجميع", "Public prices", "إظهار أسعار الكشف للمرضى والزوار.", "Show consultation prices to patients and visitors.", "Booking", true, false, null),
        new("holidayCalendar", "تقويم الإجازات", "Holiday calendar", "إغلاق العيادة في الإجازات الرسمية تلقائيا.", "Close the clinic automatically on public holidays.", "Organisation", true, false, null),
        new("workingHoursTemplates", "قوالب ساعات العمل", "Working hours templates", "قوالب جاهزة لجداول عمل الأطباء.", "Reusable working-hours templates for doctors.", "Organisation", true, false, null),
        new("patientRecords", "ملفات المرضى", "Patient records", "سجل طبي لكل مريض.", "A medical record for every patient.", "Records", true, false, null),
        new("reviews", "تقييمات المرضى", "Reviews", "تقييمات وآراء المرضى عن الأطباء.", "Patient ratings and reviews for doctors.", "Growth", true, false, null),
        new("reports", "التقارير", "Reports", "تقارير الحجوزات والإيرادات.", "Booking and revenue reports.", "Insight", true, false, null),
        new("promotions", "العروض والخصومات", "Promotions", "عروض وكوبونات خصم.", "Offers and discount coupons.", "Growth", true, false, null),
        new("recallCampaigns", "حملات الاستدعاء", "Recall campaigns", "تذكير المرضى بالزيارات الدورية.", "Remind patients about routine visits.", "Growth", true, false, null),
        new("videoConsultation", "الاستشارة بالفيديو", "Video consultation", "استشارات طبية عن بعد.", "Remote video consultations.", "Channels", true, false, null,
            "يعمل مباشرة عبر Jitsi دون مفاتيح.",
            "Works out of the box with Jitsi; no keys needed."),
        new("familyAccounts", "حسابات العائلة", "Family accounts", "حجز مواعيد لأفراد العائلة من حساب واحد.", "Book for family members from one account.", "Booking", true, false, null),
        new("insurance", "التأمين الطبي", "Insurance", "قبول شركات التأمين الطبي.", "Accept insurance providers.", "Payments", true, false, null),
    ];

    public static IReadOnlyList<FeatureDefinition> All => Entries;

    // Test hook: lets tests add a synthetic entry (e.g. an unavailable one) to exercise the coming-soon path.
    internal static void RegisterForTesting(FeatureDefinition definition) => Entries.Add(definition);

    public static FeatureDefinition? Find(string key) => All.FirstOrDefault(f => f.Key == key);

    public static IEnumerable<FeatureDefinition> Available => All.Where(f => f.Available);
}
