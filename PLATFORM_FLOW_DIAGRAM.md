# Degisim Mentorluk - Platform Flow Diagram
# TYT/AYT Focused Vertical Education Platform
# Version 1.2 - Pivot from Marketplace Model
# Generated: 2026-03-06

```
================================================================================
                     DEGISIM MENTORLUK EGITIM PLATFORMU
                  Complete Platform Architecture & User Flows
================================================================================


============================
 SECTION 1: PLATFORM OVERVIEW & ROLE MAP
============================

                          +---------------------------+
                          |     DEGISIM MENTORLUK     |
                          |   Egitim Platformu v1.2   |
                          |    (TYT/AYT Focused)      |
                          +---------------------------+
                                      |
              +----------------------------------------------+
              |                       |                      |
     +--------v--------+   +---------v--------+   +---------v--------+
     |      ADMIN       |   |     EGITMEN      |   |     OGRENCI      |
     |   (Yonetici)     |   |   (Instructor)   |   |    (Student)     |
     +------------------+   +------------------+   +------------------+
     | - User mgmt      |   | - Create content |   | - Browse content |
     | - Course review   |   | - Conduct 1:1    |   | - Book sessions  |
     | - Package mgmt   |   | - Group classes   |   | - Buy packages   |
     | - Financial ops   |   | - Video courses  |   | - Watch courses  |
     | - CMS management  |   | - Earnings       |   | - Use credits    |
     | - Feature flags   |   | - Performance    |   | - Track progress |
     | - Performance     |   | - Messages       |   | - Messages       |
     | - Accrual mgmt   |   | - Settings       |   | - Settings       |
     +------------------+   +------------------+   +------------------+


============================
 SECTION 2: AUTHENTICATION & ONBOARDING FLOW
============================

+------------------+
|   LANDING PAGE   |  (Public - Unauthenticated)
|   degisim.com    |
+--------+---------+
         |
    +----+----+
    |         |
+---v---+ +---v---+
| LOGIN | | KAYIT |  (Sign Up)
|       | | OL    |
+---+---+ +---+---+
    |         |
    |    +----v--------------------------+
    |    | Email + Password              |
    |    | Default Role: Student         |
    |    +----+--------------------------+
    |         |
    |    +----v--------------------------+
    |    | Email Verification            |
    |    +----+--------------------------+
    |         |
+---v---------v---+
|  JWT Token       |
|  Generation      |
+--------+---------+
         |
         +-------------------------------------------+
         |                    |                      |
+--------v--------+  +-------v--------+  +----------v-------+
| Role: Admin     |  | Role: Mentor   |  | Role: Student    |
| Redirect to:    |  | Redirect to:   |  | Redirect to:     |
| /admin/panel    |  | /mentor/panel  |  | Onboarding       |
+-----------------+  +--------+-------+  +----------+-------+
                              |                      |
                     +--------v--------+  +----------v------------------+
                     | Mentor          |  | Student Onboarding          |
                     | Onboarding      |  +-----------------------------+
                     | (if flag ON)    |  | Step 1: City, Gender        |
                     +--------+--------+  | Step 2: Goals (TYT/AYT)    |
                              |           | Step 3: Categories          |
                     +--------v--------+  | Step 4: Budget range        |
                     | Bio, Experience |  | Step 5: Availability prefs  |
                     | Education       |  | Step 6: Session formats     |
                     | Certifications  |  | Step 7: BirthDay/Month,     |
                     | Categories      |  |         Phone               |
                     | Session Types   |  +----------+------------------+
                     +--------+--------+             |
                              |                      |
                     +--------v--------+  +----------v------------------+
                     | ADMIN REVIEW    |  | /student/panel              |
                     | Pending...      |  | (Dashboard)                 |
                     +---+--------+----+  +-----------------------------+
                         |        |
                  +------v-+ +---v------+
                  |APPROVED| |REJECTED  |
                  |Active  | |Feedback  |
                  +--------+ +----------+

  Feature Flag: EXTERNAL_MENTOR_REGISTRATION
  +-----------------------------------------+
  | ON  -> Mentors can self-register        |
  | OFF -> Admin assigns mentor role only   |
  +-----------------------------------------+


============================
 SECTION 3: PUBLIC PAGES FLOW
============================

+------------------------------------------------------------------+
|                        PUBLIC PAGES                                |
+------------------------------------------------------------------+
|                                                                    |
|  +------------------+     +------------------+                     |
|  |  ANASAYFA        |     |  EGITMEN BUL     |                     |
|  |  (Homepage)      |     |  (Browse         |                     |
|  +------------------+     |   Instructors)   |                     |
|  | - Banners        |     +--------+---------+                     |
|  | - Modules        |              |                               |
|  | - Announcements  |     +--------v---------+                     |
|  | - Featured       |     |  Instructor      |                     |
|  |   instructors    |     |  Profile Page    |                     |
|  | - Popular        |     +--------+---------+                     |
|  |   courses        |              |                               |
|  +------------------+         +----+----+                          |
|                               |         |                          |
|                        +------v-+  +----v------+                   |
|                        | Book   |  | Send      |                   |
|                        | Session|  | Message   |                   |
|                        +--------+  +-----------+                   |
|                                                                    |
|  +------------------+     +------------------+                     |
|  |  GRUP DERSLERI   |     |  EGITIMLER       |                     |
|  |  (Group Classes) |     |  (Courses)       |                     |
|  +--------+---------+     +--------+---------+                     |
|           |                        |                               |
|  +--------v---------+     +--------v---------+                     |
|  | Class Detail     |     |  Course Detail   |                     |
|  | - Date/Time      |     |  - Sections      |                     |
|  | - Instructor     |     |  - Lectures      |                     |
|  | - Capacity       |     |  - Instructor    |                     |
|  | - Price          |     |  - Price         |                     |
|  +--------+---------+     +--------+---------+                     |
|           |                        |                               |
|  +--------v---------+     +--------v---------+                     |
|  |  ENROLL          |     |  ENROLL          |                     |
|  |  (Requires Auth) |     |  (Requires Auth) |                     |
|  +------------------+     +------------------+                     |
|                                                                    |
|  +------------------+                                              |
|  |  PAKETLER        |                                              |
|  |  (Credit         |                                              |
|  |   Packages)      |                                              |
|  +--------+---------+                                              |
|           |                                                        |
|  +--------v---------+                                              |
|  | Package Detail   |                                              |
|  | - Credit amounts |                                              |
|  | - Price          |                                              |
|  | - Validity       |                                              |
|  +--------+---------+                                              |
|           |                                                        |
|  +--------v---------+                                              |
|  |  PURCHASE        |                                              |
|  |  (Requires Auth) |                                              |
|  +------------------+                                              |
+------------------------------------------------------------------+


============================
 SECTION 4: STUDENT (OGRENCI) COMPLETE FLOW
============================

+----------------------------------------------------------------------+
| STUDENT PANEL (/student)                                              |
+----------------------------------------------------------------------+
|                                                                        |
| +--NAVIGATION BAR-------------------------------------------------+   |
| | [Genel]  [Katilimlarim]  [Hesap]         [Bildirim] [Mesaj] [PP]|   |
| +------------------------------------------------------------------+   |
|                                                                        |
| GENEL (General)                                                        |
| +-------------------+  +-------------------+                           |
| |  PANEL            |  |  MESAJLARIM       |                           |
| |  (Dashboard)      |  |  (Messages)       |                           |
| +-------------------+  +-------------------+                           |
| | - Upcoming 1:1    |  | - Conversation    |                           |
| |   sessions        |  |   list            |                           |
| | - Enrolled group  |  | - Real-time chat  |                           |
| |   classes         |  |   (SignalR)       |                           |
| | - Course progress |  | - Read receipts   |                           |
| | - Credit balance  |  | - Online status   |                           |
| +-------------------+  +-------------------+                           |
|                                                                        |
| KATILIMLARIM (My Participations)                                       |
| +-------------------+  +-------------------+  +-------------------+    |
| | BIRE BIR          |  | COKLU             |  | VIDEO             |    |
| | SEANSLARIM        |  | SEANSLARIM        |  | EGITIMLERIM       |    |
| | (1:1 Sessions)    |  | (Group Classes)   |  | (Video Courses)   |    |
| +-------------------+  +-------------------+  +-------------------+    |
| | - View bookings   |  | - Enrolled classes|  | - Enrolled courses|    |
| | - Status:         |  | - Class schedule  |  | - Watch lectures  |    |
| |   Confirmed/      |  | - Join classroom  |  | - Track progress  |    |
| |   Completed/      |  |   at scheduled    |  |   per lecture     |    |
| |   Cancelled       |  |   time            |  | - Download notes  |    |
| | - Join video      |  | - Cancel          |  | - Take exams      |    |
| |   classroom       |  |   enrollment      |  +-------------------+    |
| | - Reschedule      |  | - Refund policy:  |                           |
| | - Cancel          |  |   >24h = 100%     |                           |
| | - Leave review    |  |   2-24h = 50%     |                           |
| +-------------------+  |   <2h = 0%        |                           |
|                         +-------------------+                           |
|                                                                        |
| HESAP (Account)                                                        |
| +-------------------+  +-------------------+  +-------------------+    |
| | ODEMELERIM        |  | KREDILERIM        |  | AYARLAR           |    |
| | (Payments)        |  | (Credits)         |  | (Settings)        |    |
| +-------------------+  +-------------------+  +-------------------+    |
| | - Order history   |  | - PrivateLesson   |  | - Profile info    |    |
| | - Payment status: |  |   credit balance  |  | - Avatar          |    |
| |   Pending/Paid/   |  | - GroupLesson     |  | - Password        |    |
| |   Failed/Refunded |  |   credit balance  |  | - Notifications   |    |
| | - Receipts        |  | - VideoAccess     |  |   preferences     |    |
| +-------------------+  |   credit balance  |  +-------------------+    |
|                         | - Transaction     |                           |
|                         |   history         |                           |
|                         | - Expiration      |                           |
|                         |   dates           |                           |
|                         +-------------------+                           |
+----------------------------------------------------------------------+


============================
 SECTION 5: STUDENT BOOKING FLOW (1:1 Session)
============================

+--------+     +----------+     +----------+     +-----------+
| Browse |---->| Select   |---->| Pick     |---->| Fill      |
| Instru-|     | Offering |     | Time Slot|     | Booking   |
| ctors  |     |          |     |          |     | Questions |
+--------+     +----------+     +----------+     +-----+-----+
                                                        |
    Availability computed from:                         v
    AvailabilityTemplate (weekly rules)         +-------+-------+
    minus existing bookings                     | Create Order  |
    plus AvailabilitySlot overrides             | (type:Booking)|
    Duration + buffer considered                +-------+-------+
                                                        |
                                                        v
                                               +--------+--------+
                                               |  IYZICO PAYMENT |
                                               |  Checkout Form  |
                                               +--------+--------+
                                                        |
                                        +---------------+---------------+
                                        |                               |
                                   +----v----+                    +-----v-----+
                                   | SUCCESS |                    |  FAILURE  |
                                   +----+----+                    +-----------+
                                        |                         | Order:    |
                                        v                         | Failed    |
                              +---------+---------+               +-----------+
                              | Iyzico Callback   |
                              | --> Vercel route  |
                              | --> Backend verify|
                              +---------+---------+
                                        |
                              +---------v---------+
                              | Post-Payment:     |
                              | - Order -> Paid   |
                              | - Booking ->      |
                              |   Confirmed       |
                              | - TimeSlot ->     |
                              |   IsBooked=true   |
                              | - LedgerEntry     |
                              |   created         |
                              | - Notification    |
                              |   sent            |
                              +---------+---------+
                                        |
                                        v
                              +---------+---------+
                              | At scheduled time:|
                              | Join Video        |
                              | Classroom         |
                              | (Twilio Group)    |
                              +-------------------+


============================
 SECTION 6: STUDENT PACKAGE PURCHASE & CREDIT FLOW
============================

+----------+     +----------+     +------------+     +-----------+
| Browse   |---->| Select   |---->| Create     |---->| Iyzico    |
| Packages |     | Package  |     | Order      |     | Payment   |
|          |     |          |     | (type:     |     |           |
+----------+     +----------+     |  Package)  |     +-----+-----+
                                  +------------+           |
                                                           v
                                                  +--------+--------+
                                                  |  Payment        |
                                                  |  Verified       |
                                                  +--------+--------+
                                                           |
                                                           v
                                                  +--------+--------+
                                                  | PackagePurchase |
                                                  | Created         |
                                                  +--------+--------+
                                                           |
                                  +------------------------+------------------------+
                                  |                        |                        |
                          +-------v-------+       +--------v-------+       +--------v--------+
                          | StudentCredit |       | StudentCredit  |       | StudentCredit   |
                          | PrivateLesson |       | GroupLesson    |       | VideoAccess     |
                          | +N credits    |       | +N credits     |       | +N credits      |
                          +-------+-------+       +--------+-------+       +--------+--------+
                                  |                        |                        |
                          +-------v-------+       +--------v-------+       +--------v--------+
                          | Use for:      |       | Use for:       |       | Use for:        |
                          | 1:1 Bookings  |       | Group Class    |       | Video Course    |
                          | (deduct 1)    |       | Enrollment     |       | Access          |
                          +---------------+       | (deduct 1)     |       | (deduct 1)      |
                                                  +----------------+       +-----------------+

  Credit Lifecycle:
  +-------------------------------------------------------------------+
  | Purchase -> Distribute -> Use (Deduct) -> Expire                  |
  |                                                                    |
  | CreditTransaction types track all movements:                      |
  |   Purchase | Usage | Expiry | Refund                              |
  |                                                                    |
  | Hangfire Job: ExpireCreditJob (daily check for expired credits)   |
  +-------------------------------------------------------------------+


============================
 SECTION 7: INSTRUCTOR (EGITMEN) COMPLETE FLOW
============================

+----------------------------------------------------------------------+
| INSTRUCTOR PANEL (/mentor)                                            |
+----------------------------------------------------------------------+
|                                                                        |
| +--NAVIGATION BAR-------------------------------------------------+   |
| | [Genel] [Icerik Yonetimi] [Kazanc] [Hesap]  [Bildirim] [Mesaj] |   |
| +------------------------------------------------------------------+   |
|                                                                        |
| GENEL (General)                                                        |
| +-------------------+  +-------------------+                           |
| | PANEL             |  | MESAJLARIM        |                           |
| | (Dashboard)       |  | (Messages)        |                           |
| +-------------------+  +-------------------+                           |
| | - Stats overview  |  | - Student convos  |                           |
| | - Upcoming 1:1    |  | - Real-time chat  |                           |
| | - Upcoming group  |  |   (SignalR)       |                           |
| | - Recent reviews  |  | - Online status   |                           |
| +-------------------+  +-------------------+                           |
|                                                                        |
| ICERIK YONETIMI (Content Management)                                   |
| +-------------------+  +-------------------+                           |
| | 1:1 PAKETLERIM    |  | BIRE BIR          |                           |
| | (Offerings)       |  | SEANSLARIM        |                           |
| +-------------------+  | (1:1 Sessions)    |                           |
| | - Create offering |  +-------------------+                           |
| | - Set price       |  | - View bookings   |                           |
| | - Set duration    |  | - Accept/reject   |                           |
| | - Booking         |  | - Start video     |                           |
| |   questions       |  |   session (Twilio)|                           |
| | - Availability    |  | - Mark complete   |                           |
| |   template        |  | - No-show marking |                           |
| |   (per-offering   |  +-------------------+                           |
| |    or default)    |                                                   |
| +-------------------+                                                   |
|                                                                        |
| +-------------------+  +-------------------+                           |
| | COKLU SEANSLARIM  |  | VIDEO             |                           |
| | (Group Classes)   |  | EGITIMLERIM       |                           |
| +-------------------+  | (Courses)         |                           |
| | - Create class    |  +-------------------+                           |
| |   Title, Desc,    |  | - Create course   |                           |
| |   Category,       |  |   (Draft)         |                           |
| |   Date/Time,      |  | - Add sections    |                           |
| |   Capacity,       |  | - Add lectures    |                           |
| |   Price           |  |   (video upload   |                           |
| | - View enrollments|  |    to MinIO)      |                           |
| | - Start group     |  | - Submit for      |                           |
| |   classroom       |  |   review          |                           |
| | - Cancel class    |  | - View admin      |                           |
| |   (auto-refund)   |  |   notes           |                           |
| | - 7% platform fee |  | - Apply revisions |                           |
| +-------------------+  +-------------------+                           |
|                                                                        |
| KAZANC (Earnings)                                                      |
| +-------------------+  +-------------------+                           |
| | KAZANCLARIM       |  | PERFORMANSIM      |                           |
| | (Earnings)        |  | (Performance)     |                           |
| +-------------------+  +-------------------+                           |
| | - Earnings        |  | - Session count   |                           |
| |   overview        |  | - Video views     |                           |
| | - Payout requests |  | - Revenue summary |                           |
| | - Transaction     |  | - Monthly/weekly  |                           |
| |   history         |  |   summaries       |                           |
| +-------------------+  | - Accrual history |                           |
|                         +-------------------+                           |
|                                                                        |
| HESAP (Account)                                                        |
| +-------------------+                                                   |
| | AYARLAR           |                                                   |
| | (Settings)        |                                                   |
| +-------------------+                                                   |
| | - Profile/Bio     |                                                   |
| | - Avatar          |                                                   |
| | - Credentials     |                                                   |
| +-------------------+                                                   |
+----------------------------------------------------------------------+


============================
 SECTION 8: INSTRUCTOR SESSION & CONTENT LIFECYCLE
============================

--- 1:1 Session Lifecycle ---

  Instructor                                Student
  +--------------+                          +--------------+
  | Create       |                          | Browse       |
  | Offering     |                          | Instructors  |
  +------+-------+                          +------+-------+
         |                                         |
  +------v-------+                          +------v-------+
  | Set          |                          | Select       |
  | Availability |                          | Offering     |
  | Template     |                          +------+-------+
  +------+-------+                                 |
         |                                  +------v-------+
         |                                  | Pick Time    |
         |                                  | Slot         |
         |                                  +------+-------+
         |                                         |
         |                                  +------v-------+
         |                                  | Pay (Iyzico) |
         |                                  +------+-------+
         |                                         |
  +------v---------<------Notification------<------+
  | Booking                                        |
  | Confirmed     >------Notification------>-------+
  +------+-------+                          +------+-------+
         |                                         |
  +------v-------+                          +------v-------+
  | Start Video  | <<<< Twilio Room >>>>    | Join Video   |
  | Session      | <<<< (Group type) >>>>   | Session      |
  +------+-------+                          +------+-------+
         |                                         |
  +------v-------+                          +------v-------+
  | End Session  | ----Webhook fires---->   | Session ends |
  +------+-------+                          +------+-------+
         |                                         |
  +------v-------+                          +------v-------+
  | SessionLog   |                          | Leave Review |
  | Recorded     |                          | (1-5 stars)  |
  +--------------+                          +--------------+


--- Course Review Lifecycle ---

  +--------+      +-----------+      +----------+      +----------+
  | DRAFT  |----->| PENDING   |----->| REVISION |----->| PENDING  |--+
  |        |      | REVIEW    |      | REQUESTED|      | REVIEW   |  |
  +--------+      +-----+-----+      +----------+      +----------+  |
                        |                                             |
                   +----+----+                                        |
                   |         |                                   +----+----+
              +----v---+ +---v------+                            |         |
              |APPROVED| |REJECTED  |                       +----v---+ +---v------+
              |Published| |Feedback  |                      |APPROVED| |REJECTED  |
              +--------+ +----------+                       +--------+ +----------+
                  |
            +-----v------+
            | SUSPENDED  |  (Admin can suspend anytime)
            +-----+------+
                  |
            +-----v------+
            | ARCHIVED   |
            +------------+

  CourseStatus: Draft -> PendingReview -> Published
                                      -> RevisionRequested -> PendingReview
                                      -> Rejected
               Published -> Suspended -> Archived


============================
 SECTION 9: ADMIN COMPLETE FLOW
============================

+----------------------------------------------------------------------+
| ADMIN PANEL (/admin)                                                  |
+----------------------------------------------------------------------+
|                                                                        |
| +--ADMIN DASHBOARD----------------------------------------------+     |
| | Pending Counts:                                                |     |
| | [Mentor Approvals: N] [Course Reviews: N] [Payouts: N]       |     |
| | [Refund Requests: N]                                           |     |
| +----------------------------------------------------------------+     |
|                                                                        |
| +-USER MANAGEMENT----+  +-EDUCATION MGMT-----+  +-COURSE REVIEW--+   |
| | - List all users   |  | - Create courses   |  | - Review queue |   |
| | - Assign roles     |  |   (admin-created)  |  | - Review rounds|   |
| | - Set instructor   |  | - Assign instructor|  | - Approve/     |   |
| |   status:          |  |   to courses       |  |   Reject       |   |
| |   Active/          |  | - TYT/AYT          |  | - Admin notes  |   |
| |   Suspended/       |  |   categories       |  |   (feedback to |   |
| |   Pending          |  | - Manage sections  |  |    instructor) |   |
| | - Set owner flag   |  |   & lectures       |  | - Lecture-level|   |
| +--------------------+  +--------------------+  |   review flags |   |
|                                                  +----------------+   |
|                                                                        |
| +-PACKAGE MGMT-------+  +-INSTRUCTOR PERF----+  +-FINANCIAL------+   |
| | - Create packages  |  | - View all         |  | - Orders list  |   |
| | - Edit packages    |  |   instructor       |  | - Refund       |   |
| | - Toggle active    |  |   summaries        |  |   requests     |   |
| | - Set credit       |  | - Manage accrual   |  |   Approve/     |   |
| |   allocations:     |  |   parameters       |  |   Reject       |   |
| |   PrivateLesson    |  | - Approve/cancel   |  | - Payout       |   |
| |   GroupLesson      |  |   accruals         |  |   approvals    |   |
| |   VideoAccess      |  | - AccrualStatus:   |  | - Ledger       |   |
| | - Set price        |  |   Draft/Approved/  |  |   entries      |   |
| | - Set validity     |  |   Paid/Cancelled   |  +----------------+   |
| +--------------------+  +--------------------+                        |
|                                                                        |
| +-CMS MANAGEMENT-----+  +-FEATURE FLAGS------+  +-PLATFORM------+   |
| | - Homepage modules |  | - Toggle features  |  |  SETTINGS     |   |
| | - Banners          |  |   ON/OFF           |  | - System config|   |
| | - Announcements    |  | Key flags:         |  | - Email        |   |
| | - Static pages     |  | MARKETPLACE_MODE   |  |   templates   |   |
| +--------------------+  | EXTERNAL_MENTOR_   |  | - Notification |   |
|                          |   REGISTRATION     |  |   templates   |   |
|                          | CREDIT_SYSTEM      |  +----------------+   |
|                          | GROUP_CLASSES      |                        |
|                          +--------------------+                        |
+----------------------------------------------------------------------+


============================
 SECTION 10: PAYMENT FLOW (Iyzico Integration)
============================

                    +-------------------------------------------+
                    |           PAYMENT TRIGGER                  |
                    +-------------------------------------------+
                    | OrderType:                                 |
                    |   Booking     - 1:1 session                |
                    |   GroupClass  - Group class enrollment      |
                    |   Course      - Video course enrollment     |
                    |   Package     - Credit package purchase     |
                    +---------------------+---------------------+
                                          |
                                          v
                              +-----------+-----------+
                              |    CREATE ORDER       |
                              |    Status: Pending    |
                              +-----------+-----------+
                                          |
                                          v
                              +-----------+-----------+
                              | Generate Iyzico       |
                              | Checkout Form         |
                              | (3D Secure)           |
                              +-----------+-----------+
                                          |
                                          v
                              +-----------+-----------+
                              | User Completes        |
                              | Payment on Iyzico     |
                              +-----------+-----------+
                                          |
                         +----------------+----------------+
                         |                                 |
                    +----v----+                       +----v----+
                    | SUCCESS |                       | FAILURE |
                    +----+----+                       +----+----+
                         |                                 |
                         v                            +----v--------+
                +--------+--------+                   | Order:      |
                | Iyzico Callback |                   | Failed /    |
                | -> Vercel       |                   | Abandoned   |
                |    route.ts     |                   +-------------+
                +--------+--------+
                         |
                         v
                +--------+--------+
                | Backend         |
                | verify-callback |
                +--------+--------+
                         |
           +-------------+-------------+
           |             |             |
      +----v----+  +-----v-----+ +----v--------+
      | Order:  |  | Ledger    | | Post-Payment|
      | Paid    |  | Entry     | | Actions     |
      +---------+  | Created   | +------+------+
                   +-----------+        |
                                        |
              +-------------------------+-------------------------+
              |                    |                    |         |
     +--------v--------+ +--------v--------+ +--------v---+ +---v-----------+
     | Booking:        | | ClassEnrollment:| | Course     | | Package       |
     | Confirmed       | | Active          | | Enrollment:| | Purchase:     |
     | TimeSlot:       | |                 | | Active     | | Active        |
     | IsBooked=true   | |                 | |            | | Credits       |
     +-----------------+ +-----------------+ +------------+ | Distributed   |
                                                             +---------------+

  OrderStatus Lifecycle:
  Pending -> Paid -> (Refunded | PartiallyRefunded | Chargeback)
  Pending -> Failed
  Pending -> Abandoned (ExpirePendingOrdersJob)


============================
 SECTION 11: CREDIT SYSTEM COMPLETE FLOW
============================

+--------------------------------------------------------------------------+
|                         CREDIT SYSTEM                                     |
+--------------------------------------------------------------------------+
|                                                                            |
|  CREDIT ACQUISITION                                                        |
|  +------------------+     +-----------------+     +------------------+     |
|  |  Package         |---->| Payment         |---->| Credits          |     |
|  |  Purchase        |     | (Iyzico)        |     | Distributed      |     |
|  +------------------+     +-----------------+     +--------+---------+     |
|                                                            |               |
|                                 +-----+--------------------+----+          |
|                                 |          |                    |           |
|                          +------v---+ +---v--------+ +---------v-----+    |
|                          | Private  | | Group      | | Video         |    |
|                          | Lesson   | | Lesson     | | Access        |    |
|                          | Credits  | | Credits    | | Credits       |    |
|                          +------+---+ +---+--------+ +---------+-----+    |
|                                 |          |                    |           |
|  CREDIT USAGE                   |          |                    |           |
|                          +------v---+ +---v--------+ +---------v-----+    |
|                          | Book 1:1 | | Enroll in  | | Access Video  |    |
|                          | Session  | | Group Class| | Course        |    |
|                          | -1 credit| | -1 credit  | | -1 credit     |    |
|                          +------+---+ +---+--------+ +---------+-----+    |
|                                 |          |                    |           |
|  CREDIT TRACKING                v          v                    v           |
|                          +------+----------+--------------------+-----+    |
|                          |          CreditTransaction Log             |    |
|                          +--------------------------------------------+    |
|                          | Type: Purchase | Usage | Expiry | Refund   |    |
|                          +--------------------------------------------+    |
|                                                                            |
|  CREDIT EXPIRATION                                                         |
|  +--------------------------------------------------------------------+   |
|  |  Hangfire: ExpireCreditJob (runs daily)                            |   |
|  |  - Checks StudentCredit.ExpiresAt                                  |   |
|  |  - Creates CreditTransaction(type: Expiry)                        |   |
|  |  - Sets remaining balance to 0                                     |   |
|  +--------------------------------------------------------------------+   |
|                                                                            |
+--------------------------------------------------------------------------+


============================
 SECTION 12: VIDEO SESSION FLOW (Twilio)
============================

  INSTRUCTOR                          TWILIO                          STUDENT
  +----------+                     +-----------+                    +----------+
  | Start    |--GenerateToken--->  |           |                    |          |
  | Session  |<--Token + RoomSID-- | Create    |                    |          |
  +----+-----+                     | Room      |                    +----------+
       |                           | (Group    |
       |                           |  type)    |
  +----v-----+                     |           |
  | Connect  |--Join Room--------> |           |                    +----------+
  | to Room  |                     |           | <--GenerateToken---| Join     |
  +----+-----+                     |           | ---Token---------->| Session  |
       |                           |           |                    +----+-----+
       |                           |           |                         |
  +----v-----+                     |           |                    +----v-----+
  | Video +  | <<<< WebRTC >>>>   |           |  <<<< WebRTC >>>> | Video +  |
  | Audio +  |                     |           |                    | Audio +  |
  | Chat     | --DataTrack------> |           | --DataTrack------> | Chat     |
  | (DataTrk)|                     |           |                    | (DataTrk)|
  +----+-----+                     |           |                    +----+-----+
       |                           |           |                         |
  +----v-----+                     |           |                    +----v-----+
  | End      |--Disconnect-------> |           |                    | Auto     |
  | Session  |                     | Room      |                    | Disconn  |
  +----+-----+                     | Completed |                    +----------+
       |                           +-----+-----+
       |                                 |
       |                           +-----v-----+
       |                           | Webhook   |
       |                           | Fires     |
       |                           +-----+-----+
       |                                 |
  +----v-----+                     +-----v-----------+
  | Backend  | <---Webhook------   | room-ended      |
  | Update   |                     | participant-    |
  | Status   |                     | disconnected    |
  +----+-----+                     +-----------------+
       |
  +----v---------------------+
  | InstructorSessionLog     |
  | - Duration recorded      |
  | - VideoSession updated   |
  | - SignalR: room status   |
  |   broadcast              |
  +---------------------------+

  Room Naming Convention:
  - 1:1 sessions:    booking-{bookingId}
  - Group classes:   group-class-{classId}

  Identity Format: userId|displayName (pipe-separated)


============================
 SECTION 13: NOTIFICATION SYSTEM
============================

+--------------------------------------------------------------------------+
|                        NOTIFICATION CHANNELS                              |
+--------------------------------------------------------------------------+
|                                                                            |
|  1. IN-APP NOTIFICATIONS (Bell Icon)                                       |
|  +--------------------------------------------------------------------+   |
|  |  UserNotification Entity                                           |   |
|  |  Triggers:                                                          |   |
|  |    - New message received                                          |   |
|  |    - Booking confirmed                                             |   |
|  |    - Payment received                                              |   |
|  |    - Class enrollment                                              |   |
|  |    - Course approved/rejected                                      |   |
|  |    - Review received                                               |   |
|  |    - Payout processed                                              |   |
|  |  Flow: Action -> Create UserNotification -> Bell badge updates     |   |
|  +--------------------------------------------------------------------+   |
|                                                                            |
|  2. SIGNALR REAL-TIME                                                      |
|  +--------------------------------------------------------------------+   |
|  |  ChatHub (/hubs/chat) - JWT via query string                       |   |
|  |  Events:                                                            |   |
|  |    - ReceiveMessage          (new chat message)                    |   |
|  |    - MessageDelivered        (read receipts)                       |   |
|  |    - MessageRead             (read receipts)                       |   |
|  |    - UserOnlineStatusChanged (presence)                            |   |
|  |    - RoomStatusChanged       (video room state)                    |   |
|  |  Backend: ChatNotificationService (Api/Services/)                  |   |
|  |  Frontend: chat-connection.ts, use-signalr.ts hook                 |   |
|  +--------------------------------------------------------------------+   |
|                                                                            |
|  3. EMAIL NOTIFICATIONS                                                    |
|  +--------------------------------------------------------------------+   |
|  |  Hangfire Job: SendUnreadMessageNotificationJob                    |   |
|  |    - Runs every 2 minutes                                          |   |
|  |    - Sends email if message unread for 10+ minutes                 |   |
|  |    - Spam protection: MessageNotificationLog (24h cooldown)        |   |
|  |                                                                     |   |
|  |  Admin Bulk Notifications: BulkNotification entity                 |   |
|  +--------------------------------------------------------------------+   |
|                                                                            |
+--------------------------------------------------------------------------+


============================
 SECTION 14: PERFORMANCE & ACCRUAL SYSTEM
============================

  DATA COLLECTION
  +------------------+     +------------------+     +------------------+
  | 1:1 Sessions     |     | Group Classes    |     | Video Courses    |
  | Conducted        |     | Conducted        |     | Views/Progress   |
  +--------+---------+     +--------+---------+     +--------+---------+
           |                        |                        |
           v                        v                        v
  +--------+---------+     +--------+---------+     +--------+---------+
  | Instructor       |     | Instructor       |     | VideoWatchLog    |
  | SessionLog       |     | SessionLog       |     | LectureProgress  |
  +--------+---------+     +--------+---------+     +--------+---------+
           |                        |                        |
           +------------------------+------------------------+
                                    |
                                    v
                    +---------------+----------------+
                    |  Hangfire: Calculate            |
                    |  PerformanceSummaryJob          |
                    |  (Daily)                        |
                    +---------------+----------------+
                                    |
                                    v
                    +---------------+----------------+
                    | InstructorPerformanceSummary    |
                    | - Period: Weekly / Monthly      |
                    | - TotalSessions                 |
                    | - TotalVideoViews               |
                    | - TotalRevenue                  |
                    | - AverageRating                 |
                    +---------------+----------------+
                                    |
                                    v
                    +---------------+----------------+
                    |  Hangfire: Calculate            |
                    |  AccrualJob (Monthly)           |
                    +---------------+----------------+
                                    |
                                    v
                    +---------------+----------------+
                    | InstructorAccrual              |
                    | - InstructorId                  |
                    | - Period                        |
                    | - Amount (calculated from       |
                    |   InstructorAccrualParameter)   |
                    | - Status: Draft                 |
                    +---------------+----------------+
                                    |
                             +------+------+
                             |   ADMIN     |
                             |   REVIEW    |
                             +------+------+
                                    |
                        +-----------+-----------+
                        |                       |
                   +----v-----+          +------v-----+
                   | APPROVED |          | CANCELLED  |
                   +----+-----+          +------------+
                        |
                   +----v-----+
                   | PAID     |
                   | (Payout  |
                   |  Request)|
                   +----------+


============================
 SECTION 15: COMPLETE ENTITY RELATIONSHIP MAP
============================

  +--------+         +--------+        +-----------+
  | User   |-------->| Mentor |------->| Offering  |
  |        |         | Profile|        +-----------+
  +---+----+         +--------+        | -Price    |
      |              | -Bio   |        | -Duration |
      |              | -Exp   |        | -Type     |
      |              +--------+        +-----+-----+
      |                                      |
      |    +----------+              +-------v-------+
      +--->| Student  |              | Availability  |
      |    | Onboard  |              | Template      |
      |    | Profile  |              +-------+-------+
      |    +----------+                      |
      |                              +-------v-------+
      |    +----------+              | Availability  |
      +--->| Student  |              | Slot          |
      |    | Credit   |              +---------------+
      |    +----------+
      |    | -Private |       +----------+       +---------+
      |    | -Group   |       | Booking  |<------| Order   |
      |    | -Video   |       +----+-----+       +---------+
      |    +----------+            |             | -Type   |
      |                            |             | -Status |
      |    +----------+       +----v-----+       | -Amount |
      +--->| Message  |       | Video    |       +---------+
      |    +----------+       | Session  |
      |    | -Conver- |       +----+-----+       +---------+
      |    |  sation  |            |             | Package |
      |    +----------+       +----v---------+   +---------+
      |                       | Instructor   |   | -Credits|
      |    +----------+       | SessionLog   |   | -Price  |
      +--->| Course   |       +--------------+   +---------+
      |    | Enrollmt |                              |
      |    +----------+       +--------------+  +----v------+
      |                       | Group Class  |  | Package   |
      |    +----------+       +------+-------+  | Purchase  |
      +--->| Class    |              |          +-----------+
      |    | Enrollmt |       +------v-------+
      |    +----------+       | Class        |  +----------+
      |                       | Enrollment   |  | Review   |
      |    +----------+       +--------------+  +----------+
      +--->| User     |                         | -Rating  |
      |    | Notific. |       +--------------+  | -Comment |
      |    +----------+       | Ledger Entry |  +----------+
      |                       +--------------+
      |    +----------+       | -Direction   |  +----------+
      +--->| Credit   |       | -Amount      |  | Refund   |
           | Transact.|       | -Account     |  | Request  |
           +----------+       +--------------+  +----------+


============================
 SECTION 16: BACKGROUND JOBS (Hangfire)
============================

  +--------------------------------------------------------------------+
  |                    HANGFIRE SCHEDULED JOBS                          |
  +--------------------------------------------------------------------+
  |                                                                     |
  |  EVERY 2 MINUTES:                                                   |
  |  +--------------------------------------------------------------+  |
  |  | SendUnreadMessageNotificationJob                             |  |
  |  | - Check messages unread > 10 min                             |  |
  |  | - Send email notification                                    |  |
  |  | - Log to MessageNotificationLog (24h cooldown)              |  |
  |  +--------------------------------------------------------------+  |
  |                                                                     |
  |  PERIODIC:                                                          |
  |  +--------------------------------------------------------------+  |
  |  | ExpirePendingOrdersJob                                       |  |
  |  | - Find orders with Status=Pending past expiration            |  |
  |  | - Mark as Abandoned                                          |  |
  |  | - Release time slots                                         |  |
  |  +--------------------------------------------------------------+  |
  |                                                                     |
  |  DAILY:                                                             |
  |  +--------------------------------------------------------------+  |
  |  | ExpireCreditJob                                              |  |
  |  | - Check StudentCredit.ExpiresAt                              |  |
  |  | - Zero out expired credits                                   |  |
  |  | - Create CreditTransaction(type: Expiry)                    |  |
  |  +--------------------------------------------------------------+  |
  |  +--------------------------------------------------------------+  |
  |  | CalculatePerformanceSummaryJob                               |  |
  |  | - Aggregate InstructorSessionLog                             |  |
  |  | - Aggregate VideoWatchLog                                    |  |
  |  | - Create/update InstructorPerformanceSummary                |  |
  |  +--------------------------------------------------------------+  |
  |                                                                     |
  |  MONTHLY:                                                           |
  |  +--------------------------------------------------------------+  |
  |  | CalculateAccrualJob                                          |  |
  |  | - Read InstructorPerformanceSummary for period               |  |
  |  | - Apply InstructorAccrualParameter rates                    |  |
  |  | - Create InstructorAccrual (Status: Draft)                  |  |
  |  +--------------------------------------------------------------+  |
  |                                                                     |
  +--------------------------------------------------------------------+


============================
 SECTION 17: FEATURE FLAGS & PLATFORM MODES
============================

  +--------------------------------------------------------------------+
  |                     FEATURE FLAGS                                   |
  +--------------------------------------------------------------------+
  |                                                                     |
  |  MARKETPLACE_MODE                                                   |
  |  +------+------+                                                    |
  |  |  ON  | OFF  |                                                    |
  |  +------+------+--------------------------------------------------+|
  |  | ON:  Full marketplace with instructor discovery, public         ||
  |  |      profiles, self-service booking                             ||
  |  | OFF: Vertical education model, admin-controlled content,        ||
  |  |      package-based access                                       ||
  |  +----------------------------------------------------------------+|
  |                                                                     |
  |  EXTERNAL_MENTOR_REGISTRATION                                       |
  |  +------+------+                                                    |
  |  |  ON  | OFF  |                                                    |
  |  +------+------+--------------------------------------------------+|
  |  | ON:  Anyone can apply to become an instructor                   ||
  |  | OFF: Only admin can assign instructor role                      ||
  |  +----------------------------------------------------------------+|
  |                                                                     |
  |  CREDIT_SYSTEM                                                      |
  |  +------+------+                                                    |
  |  |  ON  | OFF  |                                                    |
  |  +------+------+--------------------------------------------------+|
  |  | ON:  Package purchase + credit-based access enabled             ||
  |  | OFF: Direct payment per session/class/course                    ||
  |  +----------------------------------------------------------------+|
  |                                                                     |
  |  GROUP_CLASSES                                                      |
  |  +------+------+                                                    |
  |  |  ON  | OFF  |                                                    |
  |  +------+------+--------------------------------------------------+|
  |  | ON:  Group classes feature available                            ||
  |  | OFF: Group classes hidden from UI                               ||
  |  +----------------------------------------------------------------+|
  |                                                                     |
  +--------------------------------------------------------------------+

  Current Pivot State (v1.2):
  +------------------------------------+
  | MARKETPLACE_MODE           = OFF   |
  | EXTERNAL_MENTOR_REGISTRATION = OFF |
  | CREDIT_SYSTEM              = ON    |
  | GROUP_CLASSES              = ON    |
  +------------------------------------+


============================
 SECTION 18: COMPLETE USER JOURNEY - STUDENT END-TO-END
============================

  1. DISCOVERY
     User visits degisim.com
     |
  2. REGISTRATION
     Click "Kayit Ol" -> Enter email/password -> Verify email
     |
  3. ONBOARDING
     Fill profile: City, Gender, Goals (TYT/AYT), Categories,
     Budget, Availability, Session formats, BirthDay/Month, Phone
     |
  4. PACKAGE PURCHASE
     Browse packages -> Select "TYT Hazirlik Paketi"
     -> Pay via Iyzico -> Receive credits:
        PrivateLesson: 10, GroupLesson: 5, VideoAccess: 3
     |
  5. BOOK 1:1 SESSION
     Browse instructors -> Select "Matematik Egitmeni"
     -> Choose "60dk TYT Matematik" offering
     -> Pick available slot: Mon 14:00-15:00
     -> Fill booking questions
     -> Confirm (1 PrivateLesson credit deducted)
     |
  6. ATTEND SESSION
     At scheduled time -> Join video classroom
     -> Video + audio + chat with instructor
     -> Session ends -> Leave 5-star review
     |
  7. JOIN GROUP CLASS
     Browse group classes -> Select "TYT Geometri Calisma Grubu"
     -> Enroll (1 GroupLesson credit deducted)
     -> At scheduled time -> Join group classroom
     |
  8. ACCESS VIDEO COURSE
     Browse courses -> Select "AYT Fizik Konu Anlatimi"
     -> Enroll (1 VideoAccess credit deducted)
     -> Watch lectures at own pace
     -> Track progress per section
     |
  9. ONGOING
     Check messages -> Review upcoming sessions
     -> Monitor credit balance -> Purchase more packages as needed
     -> View payment history -> Update settings


============================
 SECTION 19: COMPLETE USER JOURNEY - INSTRUCTOR END-TO-END
============================

  1. ASSIGNMENT
     Admin assigns instructor role to user
     OR (if flag ON) User applies -> Admin reviews -> Approved
     |
  2. PROFILE SETUP
     Fill bio, experience, education, certifications
     Set categories (e.g., TYT Matematik, AYT Fizik)
     |
  3. CREATE OFFERING
     Create "60dk Birebir TYT Matematik" offering
     -> Set price (or credit-based)
     -> Set duration + buffer
     -> Add booking questions
     -> Create/assign availability template
        (e.g., Mon-Fri 09:00-17:00)
     |
  4. RECEIVE BOOKINGS
     Student books -> Notification received
     -> View booking details
     |
  5. CONDUCT SESSION
     At scheduled time -> Start video session (Twilio)
     -> Conduct lesson -> End session
     -> InstructorSessionLog recorded
     |
  6. CREATE GROUP CLASS
     "TYT Geometri Calisma Grubu"
     -> Set date, time, capacity (max 20), price
     -> Students enroll -> At scheduled time
     -> Start group classroom -> Conduct class -> Complete
     |
  7. CREATE VIDEO COURSE
     "AYT Fizik Konu Anlatimi" (Draft)
     -> Add sections: "Mekanik", "Optik", "Dalgalar"
     -> Add lectures with video upload (MinIO)
     -> Submit for review (PendingReview)
     -> Admin reviews -> Approved/RevisionRequested
     -> Published -> Students can enroll
     |
  8. TRACK EARNINGS
     View earnings overview
     -> Monthly performance summary generated (Hangfire)
     -> Monthly accrual calculated -> Admin approves
     -> Payout request -> Payment processed
     |
  9. ONGOING
     Reply to student messages
     -> Monitor performance metrics
     -> Update offerings/availability
     -> Create more content


============================
 SECTION 20: TECHNOLOGY STACK DIAGRAM
============================

  +--FRONTEND (Vercel)----------------------------------+
  |  Next.js / React                                     |
  |  +-------------+  +----------+  +-----------+       |
  |  | Pages/      |  | API      |  | SignalR   |       |
  |  | Components  |  | Client   |  | Client    |       |
  |  +-------------+  +----------+  +-----------+       |
  +---------------------------+-----------+------+-------+
                              |           |      |
                         HTTPS/REST    WebSocket  |
                              |           |      |
  +--BACKEND (Koyeb)----------+-----------+------+-------+
  |  ASP.NET Core 8.0                                    |
  |  +----------+  +----------+  +----------+            |
  |  | Api      |  | Applicat.|  | Domain   |            |
  |  | Control- |  | Commands |  | Entities |            |
  |  | lers     |  | Queries  |  | Enums    |            |
  |  | Hubs     |  | Valid.   |  | Events   |            |
  |  +----------+  +----------+  +----------+            |
  |  +----------+  +----------+  +----------+            |
  |  | Persist. |  | Infra.   |  | Identity |            |
  |  | EF Core  |  | Twilio   |  | JWT      |            |
  |  | Configs  |  | Iyzipay  |  | Tokens   |            |
  |  | Migrat.  |  | MinIO    |  +----------+            |
  |  +----+-----+  | Email    |                          |
  |       |        | Hangfire |                          |
  |       |        +----+-----+                          |
  +-------+-------------+-----------+--------------------+
          |              |           |
  +-------v---+  +------v----+ +----v------+  +----------+
  | PostgreSQL|  | Twilio    | | Iyzico    |  | MinIO    |
  | (Neon)    |  | Video API | | Payment   |  | (S3)     |
  +-----------+  +-----------+ | Gateway   |  | Storage  |
                               +-----------+  +----------+
  +----------+
  | Redis    |
  | Cache    |
  +----------+

================================================================================
                              END OF DIAGRAM
================================================================================
```
