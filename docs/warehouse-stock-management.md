# صفحه و APIهای مدیریت موجودی انبارها — مرحلهٔ سوم

صفحهٔ «موجودی انبارها» در مسیر `/dashboard/inventory/warehouses` اضافه شده است. این صفحه از صفحهٔ داده‌های پایهٔ انبارها مستقل است و در منوی برنامه فقط با مجوز مشاهده نمایش داده می‌شود. نسخهٔ واقعی پروژه (.NET 10، Vue 3 و قالب Bootstrap/DataTables موجود) حفظ شده است.

## قابلیت‌ها

- انتخاب و جستجوی انبار، جستجوی کد یا عنوان کالا، فیلتر دارای موجودی/بدون موجودی، مرتب‌سازی، صفحه‌بندی سمت سرور و تازه‌سازی.
- منبع فهرست `Product` است؛ کالای بدون رکورد موجودی با مقدار صفر و نسخهٔ `null` نمایش داده می‌شود.
- موجودی فعلی از `WarehouseStockBalance` و موجودی اولیه از تجمیع گردش‌های `OpeningBalance` همان انبار خوانده می‌شود. فهرست، کل تاریخچه را برای محاسبهٔ موجودی جاری جمع نمی‌زند.
- فرم چندردیفی موجودی اولیه از `ProductSelect` موجود و جستجوی محدودشدهٔ سرور استفاده می‌کند.
- فرم اصلاح، کالا و انبار ثابت، موجودی فعلی، موجودی نهایی، اختلاف خودکار و علت الزامی دارد.
- در موبایل، کد و واحد کنار عنوان و مقدار نمایش داده می‌شوند؛ فقط ستون‌های مهم و عملیات باقی می‌مانند. استایل‌ها از متغیرهای تم فعلی استفاده می‌کنند.

## APIها و دسترسی‌ها

| روش و مسیر | دسترسی |
|---|---|
| `GET /endpoint/master-data/warehouses/options` | `WarehouseStockView` |
| `GET /endpoint/inventory/warehouses/{warehouseId}/stocks` | `WarehouseStockView` |
| `POST /endpoint/inventory/opening-balances` | `WarehouseOpeningBalanceManage` |
| `POST /endpoint/inventory/adjustments` | `WarehouseInventoryAdjustmentManage` |

پارامترهای فهرست: `pageNumber`، `pageSize` (۱ تا ۱۰۰)، `search`، `hasStock`، `sortBy`، `sortDescending` و `productId` اختیاری. پارامتر آخر برای دریافت نسخهٔ تازهٔ یک کالا پس از تداخل استفاده می‌شود. شناسهٔ انبار از مسیر Endpoint گرفته می‌شود.

سه مجوز در `AppPermission` و گروه مستقل «موجودی انبارها» در `PermissionPolicyNames` ثبت شده‌اند. سیاست‌ها و Seed نقش مدیر از سازوکار موجود پروژه استفاده می‌کنند. مجوزهای ورود یا خروج کالا دسترسی نوشتن موجودی ایجاد نمی‌کنند. داشتن مجوز مشاهده هم به‌تنهایی اجازهٔ نوشتن نمی‌دهد. جستجو و دریافت کالای انتخاب‌شده برای دارندهٔ مجوز موجودی اولیه مجاز شده، بدون اعطای مجوز مدیریت داده‌های پایه.

## قوانین ثبت

ثبت موجودی اولیه و اصلاح از طریق `IInventoryMovementService` و `InventoryMovementService` مشترک انجام می‌شوند. Handlerها تراکنش Serializable را مدیریت می‌کنند و گردش و موجودی جاری را با هم ذخیره می‌کنند.

- هر درخواست موجودی اولیه حداکثر ۱۰۰ کالای متمایز دارد. کالاها و انبار باید وجود داشته باشند. مقدار منفی یا بیش از سه رقم اعشار پذیرفته نمی‌شود.
- وجود هر گردش قبلی برای کالا/انبار، یا موجودی مثبت موجود بدون گردش، مانع موجودی اولیه است. این بررسی داخل تراکنش انجام می‌شود؛ رقابت درخواست‌ها باعث دوباره‌شماری نمی‌شود.
- مقدار صفر هیچ گردش یا رکورد موجودی ایجاد نمی‌کند و حق ثبت اولیه را رزرو نمی‌کند. همین سیاست در راهنمای فرم نمایش داده می‌شود.
- خطای هر ردیف باعث رد کل درخواست می‌شود. Entity مستقل برای موجودی اولیه یا اصلاح ایجاد نشده است.
- مدل فعلی `OperationId` یکتا برای هر گردش دارد؛ بنابراین ردیف‌های یک درخواست چندکالایی شناسه‌های عملیات جداگانه دارند. همهٔ ردیف‌ها همچنان در یک تراکنش ذخیره می‌شوند.
- اصلاح، اختلاف مقدار نهایی با موجودی واقعی را در سرور محاسبه می‌کند و علت را در `StockMovement.Description` ثبت می‌کند. اختلاف صفر گردش تازه ایجاد نمی‌کند.
- برای اصلاح، هم `ExpectedQuantity` و هم `RowVersion` بررسی می‌شوند. برای کالای بدون رکورد، مقدار مورد انتظار صفر و نسخه `null` است؛ ایجاد هم‌زمان اولین رکورد نیز با تراکنش و ایندکس یکتای کالا/انبار محافظت می‌شود.
- تداخل نسخه یا شکست رقابت تراکنش با پاسخ 409 مدیریت می‌شود. فرم، ذخیره را متوقف می‌کند و امکان دریافت موجودی تازه و بررسی دوباره را می‌دهد.
- گردش‌های قبلی ویرایش یا حذف نمی‌شوند. زمان گردش، زمان واقعی اعمال موجودی است؛ اطلاعات کاربر و زمان ایجاد از Audit موجود می‌آیند.

## Migration و داده‌های قبلی

Migration جدید `20261105083109_IndexWarehouseStockMovements` ایندکس ترکیبی `WarehouseId + ProductId + MovementType` را جایگزین ایندکس تک‌ستونی انبار می‌کند تا جستجوی سوابق و موجودی اولیه بهینه باشد. داده‌ای تولید، بازنویسی یا حدس زده نمی‌شود.

Migration مرحلهٔ قبل (`AddReceiptInventoryAndOptionalWarehouse`) نیز پیش‌نیاز ساخت جدول‌های موجودی است. Migrationها روی دیتابیس اصلی اجرا نشده‌اند. اسناد ورود بدون انبار در این صفحه اثری ندارند و دادهٔ تاریخی ساختگی برای تکمیل موجودی ایجاد نشده است.

## فایل‌های مرحلهٔ سوم

فایل‌های جدید:

- `Warehouse.Application/Features/Inventory/WarehouseStocks/GetWarehouseStocksQuery.cs`
- `Warehouse.Application/Features/Inventory/WarehouseStocks/StockCommands.cs`
- `Warehouse.Server/Controllers/WarehouseStocksController.cs`
- `Warehouse.client/src/services/warehouseStocks.ts`
- `Warehouse.client/src/views/Inventory/warehouse-stocks/WarehouseStocksView.vue`
- `Warehouse.IntegrationTests/Persistence/WarehouseStockManagementTests.cs`
- `Warehouse.Server.Tests/Integration/WarehouseStockPermissionTests.cs`
- دو فایل Migration مربوط به `IndexWarehouseStockMovements`.

فایل‌های تکمیل‌شده:

- `Warehouse.Application/Common/Interfaces/IInventoryMovementService.cs`
- `Warehouse.Application/Features/Inventory/InventoryMovementService.cs`
- `Warehouse.Application/Common/Authorization/AppPermission.cs`
- `Warehouse.Application/Common/Authorization/PermissionPolicyNames.cs`
- `Warehouse.Server/Controllers/WarehousesController.cs`
- `Warehouse.Server/Controllers/ProductsController.cs`
- `Warehouse.Infrastructure/Persistence/ApplicationDbContext.cs`
- `Warehouse.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`
- `Warehouse.client/src/router/index.ts`
- `Warehouse.client/src/layouts/DashboardLayout.vue`

صفحات و APIهای تحویل به واحد، دریافت از واحد و انتقال انبار ایجاد نشده‌اند.

## اعتبارسنجی

- Build کامل Solution و Build/TypeScript فرانت‌اند اجرا شده‌اند. Build کامل با هشدارهای وابستگی‌های کلاینت و بدون خطای کامپایل پایان یافته است.
- نتیجهٔ اجرا: ۳۱۲ تست Application، ۱۹۱ تست Server و ۲۷ تست SQL Server مربوط به موجودی و رگرسیون ورودی کالا موفق بودند. از این مجموعه، ۱۳ تست دیتابیس و ۹ تست HTTP برای قابلیت‌های مرحلهٔ سوم اضافه شدند.
- تست‌های جدید SQL Server شامل جدایی انبارها، نمایش صفر، جستجو/صفحه‌بندی، ثبت چندردیفی، ثبت تکراری، صفر، اصلاح مثبت/منفی، عدم تغییر، نسخهٔ قدیمی و رقابت درخواست‌ها هستند.
- تست‌های HTTP، مجوز مستقل هر عملیات و رد درخواست مستقیم فاقد مجوز را بررسی می‌کنند.
- بررسی مرورگر با داده‌های Mock جدا از دیتابیس اصلی انجام شد: دارک/روشن، موبایل، تغییر انبار، ثبت اولیه، اصلاح پس از تداخل، مخفی بودن عملیات برای مشاهده‌گر و محافظت از Route.
