<div align="center">
  <img src="docs/assets/hero.png" alt="نقلة — اسحب. اضغط Shift. انتهى." width="100%">

  <br>

  **اسحب ملفاً. اضغط Shift. كل ما تحتاجه يظهر حوله.**

  أداة Windows تعمل في شريط النظام وتحوّل أي ملف محلي إلى عجلة أوامر.<br>
  بلا نافذة رئيسية، بلا رفع، بلا تتبّع.

  <br>

  ![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-0078D4?style=flat-square&logo=windows11&logoColor=white)
  ![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
  ![Version](https://img.shields.io/badge/version-0.8%20development-FFB347?style=flat-square)
  ![License](https://img.shields.io/badge/license-MIT-55D6B0?style=flat-square)
  ![Offline](https://img.shields.io/badge/processing-100%25%20local-111827?style=flat-square)

  <br>

  [English](README.md) · [التدقيق الكامل](docs/FINAL-FEATURE-AUDIT.md) · [القيود](docs/KNOWN-LIMITATIONS.md) · [الأمان](docs/SECURITY-REVIEW.md)
</div>

<div dir="rtl">

> [!WARNING]
> **نسخة تطوير 0.8.** المعالجة المحلية الفعلية وأدلة القبول الآلي موجودة، لكن اختبار Windows الفعلي والتثبيت على حساب نظيف ما زالا معلّقين. هذه ليست نسخة مستقرة عامة.

## كيف تعمل

| | |
|:--:|---|
| **1** | **اسحب** ملفاً مدعوماً من Explorer أو سطح المكتب. |
| **2** | **استمر** بضغط الزر الأيسر ثم اضغط **Shift**. |
| **3** | **أفلت** فوق القطاع. تبدأ المعالجة أو يفتح المحرر المصغّر. |

تأتي بيانات العجلة من محتوى OLE الفعلي، ولا يخمّنها اختصار لوحة المفاتيح. اضغط **Ctrl** للأدوات، و**Tab** / **Shift+Tab** للتنقل بين القطاعات، و**Esc** للإلغاء.

## عجلة واحدة، لكل نوع ملف

<img src="docs/assets/wheel-family.png" alt="عجلة نقلة للصور والفيديو والصوت وPDF والأدوات" width="100%">

> العجلة نفسها، بألوان وقطاعات مختلفة لكل عائلة. يعرض المركز نوع الملف وحجمه الحقيقيين.

<details>
<summary><b>ورقة الحالات الكاملة</b> — التحويل، التمرير، الأدوات والتنقل</summary>
<br>
<img src="docs/assets/wheel-states.png" alt="حالات عجلة نقلة والتمرير وصفحات الأدوات" width="100%">
</details>

## ماذا تفعل

| الفئة | الإمكانات |
|---|---|
| **الصور** | تحويل JPG · PNG · WEBP · AVIF · TIFF · HEIC · PDF، قص، تدوير، ضغط، محسّن الحجم المستهدف |
| **الفيديو** | MP4 · MOV · WebM · GIF · استخراج MP3/M4A، استخراج الإطارات، قص، ترجمة |
| **الصوت** | MP3 · M4A · WAV · FLAC · AIFF · OPUS، تطبيع الجهارة بمرورين، كشف الصمت، أدوات القنوات |
| **PDF** | دمج، استخراج، حذف، إعادة ترتيب، تدوير، علامة مائية، أرقام صفحات؛ تصدير إلى PNG/JPG/TXT/DOCX/RTF |
| **المستندات** | DOCX · RTF · TXT ↔ PDF واستخراج النص الأصلي |
| **الملفات** | Smart Rename مع معاينة وتراجع، ZIP/7Z/TAR، تنظيف الخصوصية |

مصفوفة القبول تغطي **136** زوج تحويل حقيقي. HEIC وOCR وعزل الخلفية والترجمة وكتابة 7Z وتنظيف qpdf وتصدير Office كلها **حزم محلية اختيارية** — بلا تنزيل تلقائي.

## أدوات لا تظهر إلا عند الحاجة

<img src="docs/assets/tool-editors.png" alt="محررات نقلة المصغّرة: الإطارات، القص، التطبيع، الترجمة، OCR، الخصوصية" width="100%">

> محررات صغيرة وسياقية تُفتح فقط عندما تحتاج الأداة إلى إعدادات. لا لوحة تحكم ولا نافذة عند البدء.

## صندوق PDF حقيقي

<p align="center"><img src="docs/assets/pdf-toolkit.png" alt="صندوق أدوات PDF في نقلة مع معاينة مباشرة" width="62%"></p>

> سبع عمليات تحافظ على المتجهات، مع مصغرات وتكبير وملاءمة وتحريك. تُستورد متجهات الصفحات الموجودة، ويُرسم النص العربي الجديد كمخططات.

## محلي بالتصميم

| | |
|---|---|
| **OLE وليس تخميناً** | بيانات سحب حقيقية من Explorer مع خطافات أصلية أثناء الإيماءة. |
| **معالجة دون اتصال** | التطبيق المثبّت لا يرفع الملفات ولا ينزّل النماذج. |
| **مخرجات آمنة** | أسماء جديدة بجانب المصدر. Smart Rename يغيّر الأصل فقط بعد تأكيد صريح. |
| **تقدم صادق** | إخفاقات لكل ملف وإلغاء، مع الحفاظ على النتائج المكتملة. |
| **تشخيص هادئ** | سجلات محلية مصنّفة بلا مسارات أو أسماء أو محتوى. |

## البناء من المصدر

**المتطلبات:** Windows 10/11 x64 و.NET 10 SDK. ملف `global.json` يثبّت `10.0.102` مع roll-forward.

```powershell
git clone https://github.com/RealFiras/naqla.git
cd naqla
dotnet build Naqla.sln -c Release
dotnet run --project src/Naqla.App -c Release
```

المحركات الاختيارية تُجلب يدوياً أثناء التطوير فقط؛ المعالجة العادية لا تحتاج الشبكة:

```powershell
scripts\fetch-media-engine.ps1
scripts\fetch-heif-engine.ps1
py -3 scripts\fetch-optional-components.py
```

حزم Office وqpdf تحتاج مدخلاتها المثبّتة. راجع [الاعتماديات](docs/DEPENDENCIES.md).

## الاختبارات

```powershell
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/my-tests --release-acceptance
```

كل تشغيل يحتاج مجلد نتائج جديداً ومملوكاً. للاختبارات الموسّعة:

```powershell
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/stress-tests --stress-only
dotnet run --project tests/Naqla.Tests -c Release -- artifacts/pack-tests --component-packs-only
```

## الحالة

| البوابة | النتيجة |
|---|---|
| مصفوفة التحويل | **136/136** زوجاً |
| القبول الآلي | **123/123** مجموعة بعد إعادة تشغيل موثّقة لعزل عيّنة |
| Windows الفعلي / DPI / شريط النظام | **بانتظار الاختبار اليدوي** |
| تثبيت / ترقية / إزالة على حساب نظيف | **بانتظار الاختبار اليدوي** |
| التشغيل على شبكة مفصولة | **بانتظار الاختبار اليدوي** |

الأدلة الكاملة وكل إخفاق محفوظ في [تقرير نهاية التنفيذ](docs/END-TO-END-TEST-REPORT.md). الحدود المعروفة في [KNOWN-LIMITATIONS.md](docs/KNOWN-LIMITATIONS.md).

## التوثيق

| | |
|---|---|
| [التدقيق الكامل](docs/FINAL-FEATURE-AUDIT.md) | 180 صفاً للنطاق والقبول |
| [التحويلات](docs/CONVERSIONS.md) | أزواج المصدر والهدف المدعومة |
| [Smart Rename](docs/SMART-RENAME.md) | المعاينة والتحويلات والتعارضات والتراجع |
| [الحجم المستهدف](docs/TARGET-SIZE.md) | الترميز التكراري والحدود الصارمة |
| [صندوق PDF](docs/PDF-TOOLKIT.md) | العمليات السبع وقواعد الحفظ |
| [الإضافات](docs/PLUGINS.md) | واجهة الإضافات المحلية التصريحية |
| [مراجعة الأمان](docs/SECURITY-REVIEW.md) | نموذج التهديد وحدود العمليات |
| [الاعتماديات](docs/DEPENDENCIES.md) | المحركات المثبّتة والتراخيص والحزم |
| [الاختبارات اليدوية](docs/MANUAL-WINDOWS-TESTS.md) | قائمة قبول Windows الفعلي |

## الترخيص

كود نقلة منشور تحت [ترخيص MIT](LICENSE). المكوّنات الخارجية تحتفظ بتراخيصها؛ راجع [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) و[docs/DEPENDENCIES.md](docs/DEPENDENCIES.md). لا يوجد ادعاء بشهادة توزيع عام أو امتثال كامل للمصدر.

<div align="center">
  <sub>مصمّمة لـ Windows. تختفي حتى تحتاجها.</sub>
</div>
