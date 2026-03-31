# DANH SÁCH LỖI - CHO THUÊ XE PROJECT
## Format: [LOẠI LỖI] | File | Dòng/Vị trí | Mô tả lỗi cụ thể

---

## 2A. LỖI CONTROLLER / ACTION

### Missing Action Methods

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| MISSING ACTION | Views/Rental/Book.cshtml | 331 | Form gọi `asp-action="Create" asp-controller="Rental" method="post"` nhưng RentalController KHÔNG có Create() action. Chỉ có Rent() method. |
| MISSING ACTION | Views/Customer/Profile.cshtml | 366 | Form gọi `asp-action="Update" asp-controller="Customer" method="post"` nhưng CustomerController KHÔNG có Update(UpdateProfileInputModel) action. |
| MISSING ACTION | Views/Customer/Profile.cshtml | 409 | Form gọi `asp-action="ChangePassword" asp-controller="Customer" method="post"` nhưng CustomerController KHÔNG có ChangePassword() action. |
| MISSING ACTION | Views/Home/Contact.cshtml | 305 | Form gọi `asp-action="SendMessage"` nhưng HomeController KHÔNG có SendMessage(string content) action. |
| MISSING ACTION | Views/Rental/Search.cshtml | 365 | Form gọi `asp-action="Search"` nhưng RentalController KHÔNG có Search() action. |
| MISSING ACTION | Views/Employee/Vehicles.cshtml | 598 | Form gọi `asp-action="CreateVehicle"` nhưng EmployeeController KHÔNG có CreateVehicle() action. Chỉ AdminController.AddVehicle tồn tại. |

### Missing Supporting Actions

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| MISSING ACTION | Controllers/CustomerController.cs | N/A | KHÔNG có AddFavorite(int vehicleId) action mặc dù View có form gọi. |
| MISSING ACTION | Controllers/CustomerController.cs | N/A | KHÔNG có ToggleFavorite(int vehicleId) action. |
| MISSING ACTION | Controllers/CustomerController.cs | N/A | KHÔNG có AddReview(VehicleReviewInputModel) action mặc dù VehicleReviewInputModel tồn tại. |
| MISSING ACTION | Controllers/CustomerController.cs | N/A | KHÔNG có ShowContract(int contractId) action để hiển thị chi tiết hợp đồng. |

### Duplicate / Conflicting Actions

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| DUPLICATE ACTION | Controllers/RentalController.cs | 102-124 | Cả hai CreateDraft(int customerId, int? employeeId) [HttpGet] và CreateDraftPost(CreateDraftInputModel) [HttpPost] tồn tại. Nên consolidate thành một POST-only action. |
| ACTION SIGNATURE MISMATCH | Controllers/RentalController.cs | 104 | CreateDraft() nhận FromRoute parameters nhưng View gửi via form POST. Routing không match. |

### Missing ValidateAntiForgeryToken

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| SECURITY - CSRF | Controllers/EmployeeController.cs | 88 | ApproveContract() [HttpPost] KHÔNG có [ValidateAntiForgeryToken]. CSRF vulnerability. |

---

## 2B. LỖI MODEL / SCHEMA

### Missing Model Classes

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| MODEL MISSING | Models/Portal/ | N/A | ContractFullViewModel KHÔNG tồn tại. Được sử dụng bởi: GetContractsAsync(), GetContractsByCustomerAsync(), AdminDashboardViewModel.Contracts, CustomerDashboardViewModel.Contracts. CRITICAL. |
| MODEL MISSING | Models/Rental/ | N/A | RentalDashboardViewModel KHÔNG tồn tại. Được trả về bởi RentalController.BuildDashboardAsync() nhưng KHÔNG định nghĩa. CRITICAL. |
| MODEL MISSING | Models/Portal/ | N/A | PendingContractViewModel KHÔNG tồn tại. Được sử dụng bởi: GetPendingContractsAsync(), GetPendingContractsByCustomerAsync(), EmployeeDashboardViewModel.PendingContracts, CustomerDashboardViewModel.PendingContracts. CRITICAL. |
| MODEL MISSING | Models/Portal/ | N/A | BrandOptionViewModel KHÔNG tồn tại. Được trả về bởi GetBrandsAsync(). Chỉ có TypeOptionViewModel nhưng KHÔNG có BrandOptionViewModel. |
| MODEL MISSING | Models/Portal/ | N/A | EmployeeDashboardViewModel KHÔNG tồn tại hoặc KHÔNG đầy đủ. Được trả về bởi EmployeeController.Index() nhưng chỉ tìm thấy partial definition. |
| MODEL MISSING | Models/Portal/ | N/A | AdminVehicleOccupancyViewModel KHÔNG tồn tại. Được sử dụng bởi AdminDashboardViewModel.VehicleOccupancies. |
| MODEL MISSING | Models/Portal/ | N/A | PendingProfileUpdateRequestViewModel KHÔNG tồn tại hoặc KHÔNG đầy đủ. Được sử dụng bởi AdminDashboardViewModel.PendingProfileUpdates. |

### Model Property Mismatches

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| PROPERTY MISSING | Models/Portal/CreateVehicleInputModel.cs | ~ | Form input "LicensePlate" gọi tới Model property LicensePlate ✓ OK. |
| PROPERTY MISMATCH | Models/Rental/RentVehicleInputModel | N/A | View/Controller references RentVehicleInputModel nhưng model KHÔNG tìm thấy file. |
| PROPERTY MISSING | Models/Portal/UpdateProfileInputModel.cs | ~ | Chỉ có FullName, Phone nhưng View có thêm fields không match. |

### Model Property Types

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| TYPE MISMATCH | Data/RentalRepository.cs | ~1050 | GetPendingVerificationsAsync() trả về int?.CccdDocumentId và int?.DriverLicenseDocumentId nhưng Model chỉ định là int?, data conversion may fail. |

---

## 2C. LỖI MIGRATION / DATABASE

### Missing Migrations

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| NO MIGRATIONS | Project/Migrations/ | N/A | KHÔNG CÓ thư mục Migrations/. Dự án sử dụng database-first approach với raw SQL thay vì EF Core migrations. Schema changes NOT versioned. |
| SCHEMA UNVERSIONED | Database/ | ~ | Có các file .sql (init_feature_schema.sql, feature_messages_amenities.sql) nhưng KHÔNG integrate với EF Core. |

### Missing/Undefined Tables

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| TABLE SCHEMA UNDEFINED | Data/AuthRepository.cs | 118 | otp_codes table được INSERT/SELECT nhưng schema NOT verified. Columns: otp_id, user_id, email, otp_code, expires_at, is_used, created_at. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~60 | vehicle_images table được SELECT nhưng cấu trúc columns KHÔNG documented. Assumed: (image_id, vehicle_id, image_url). |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~500 | documents table được tham chiếu nhưng schema undefined. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~1500 | driver_licenses table tham chiếu nhưng cấu trúc KHÔNG rõ. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~ | favorite_vehicles table có TRY-CATCH cho missing table error nhưng NOT officially documented. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~ | notifications table tham chiếu nhưng schema KHÔNG documented. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~ | support_messages table tham chiếu nhưng cấu trúc KHÔNG clear. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~ | vehicle_reviews table tham chiếu nhưng schema KHÔNG documented. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~ | profile_update_requests table tham chiếu nhưng cấu trúc undefined. |
| TABLE SCHEMA UNDEFINED | Data/RentalRepository.cs | ~ | activity_logs table tham chiếu nhưng NEVER populated (LogActivityAsync không implement). |

### Missing Database Views

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| VIEW SCHEMA UNDEFINED | Data/RentalRepository.cs | 40 | vw_vehicle_detail view được SELECT nhưng cấu trúc columns KHÔNG documented. Expected columns: vehicle_id, vehicle_name, brand_name, type_name, price_per_day. |
| VIEW SCHEMA UNDEFINED | Data/RentalRepository.cs | 300 | vw_contract_full view được SELECT nhưng schema KHÔNG documented. Expected columns: contract_id, full_name, vehicle_name, start_date, end_date, total_amount, status. |
| VIEW SCHEMA UNDEFINED | Data/RentalRepository.cs | 350 | vw_revenue view được SELECT nhưng schema KHÔNG documented. Expected: vehicle_name, total_revenue. |

### Stored Procedures

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| SPROC SIGNATURE UNCLEAR | Data/RentalRepository.cs | 560 | CreateContractDraftAsync() gọi sp_create_contract nhưng Oracle procedure signature NOT documented. Parameters assumed but NOT verified. |
| SPROC SIGNATURE UNCLEAR | Data/RentalRepository.cs | ~600 | RentVehicleAsync() gọi sp_rent_vehicle nhưng signature undefined. Trigger comment suggests auto-calculation. |
| SPROC SIGNATURE UNCLEAR | Data/RentalRepository.cs | ~700 | MakePaymentAsync() gọi sp_make_payment nhưng signature undefined. |
| SPROC SIGNATURE UNCLEAR | Data/RentalRepository.cs | ~800 | ApproveContractAsync() gọi sp_approve_contract nhưng signature undefined. |

---

## 2D. LỖI KHÁC

### Architecture / Design Issues

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| ARCHITECTURE | Data/RentalRepository.cs | All | Sử dụng direct OracleConnection/OracleCommand instead of Entity Framework Core DbContext. KHÔNG có type safety, automatic migrations, lazy loading. Harder to maintain & test. |
| ARCHITECTURE | Data/AuthRepository.cs | All | Plaintext password storage: `password = :p_password` - KHÔNG hashing. CRITICAL security issue. |
| ARCHITECTURE | Controllers/RentalController.cs | ~200 | BuildDashboardAsync() method nhưng NEVER called. Unused code. |

### Code Duplication

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| DUPLICATE CODE | Controllers/RentalController.cs | 90-124 | CreateDraft (GET) & CreateDraftPost (POST) DUPLICATE logic. Should consolidate. |
| DUPLICATE CODE | Controllers/CustomerController.cs | ~160 | Preview & Rent action có duplicate validation logic. Extract to helper. |
| DUPLICATE CODE | Data/RentalRepository.cs | 200-250 | BuildVehicleSearchSql() & BuildVehicleFallbackSql() có similar structure. DRY violation. |

### Inconsistent Error Handling

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| INCONSISTENT ERROR | Controllers/RentalController.cs | 50 | Details() có try-catch OracleException nhưng Preview() chỉ partial error handling. |
| INCONSISTENT ERROR | Controllers/AdminController.cs | 20 | Index() catch Exception generic nhưng AddVehicle() catch OracleException specific. Inconsistent. |
| INCONSISTENT ERROR | Data/RentalRepository.cs | 150 | GetFavoriteVehiclesByCustomerAsync() có catch missing table nhưng GetVehiclesAsync() không. |

### Incomplete Implementations

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| INCOMPLETE | Data/RentalRepository.cs | 900 | LogActivityAsync(int? userId, string action, string details) - Method signature exists nhưng NEVER implemented. Activity logs NOT recorded. |
| INCOMPLETE | Data/RentalRepository.cs | ~1000 | GetPendingProfileUpdateRequestsAsync() được call nhưng implementation KHÔNG verify. |
| INCOMPLETE | Controllers/CustomerController.cs | ~350 | TempData messages sử dụng "Info" key nhưng View KHÔNG check for this key. Message displays may fail. |

### Missing Input Validation

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| VALIDATION MISSING | Controllers/RentalController.cs | 130 | RentVehicleInputModel KHÔNG verify EmployeeId tồn tại. Hardcoded fallback to ID 3. |
| VALIDATION MISSING | Controllers/AdminController.cs | 120 | AddVehicle() SetOwnerId = 0 sẽ gây error. Should validate > 0. |
| VALIDATION MISSING | Data/AuthRepository.cs | 50 | RegisterCustomerAsync() KHÔNG check password strength server-side (client-side regex in model). |

### Duplicate View Files

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| DUPLICATE VIEW | Views/Rental/Details.cshtml vs Detail.cshtml | N/A | Hai file với tên tương tự. Routing ambiguity. Nên remove một. |
| DUPLICATE VIEW | Views/Admin/Dashboard.cshtml vs AdminDashboard.cshtml | N/A | Hai file similar names. AdminController.Index() return View("Dashboard") - confusing. |

### Unverified External Dependencies

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| EXTERNAL DEP | Services/EmailService.cs | N/A | EmailService gọi SMTP nhưng config incomplete. appsettings.json có placeholder values. |
| EXTERNAL DEP | Program.cs | 10 | AddScoped<IRentalRepository, RentalRepository>() - chỉ 1 implementation. No interface segregation cho different concerns. |

### Missing View Models

| LOẠI LỖI | File | Dòng | Mô tả lỗi |
|----------|------|------|----------|
| VIEW MODEL UNCLEAR | Views/Rental/Compare.cshtml | N/A | View tồn tại nhưng KHÔNG rõ Model type. Assumed complex object nhưng NOT documented. |
| VIEW MODEL UNCLEAR | Views/Rental/Recommendations.cshtml | N/A | View tồn tại nhưng model type KHÔNG documented. |
| VIEW MODEL UNCLEAR | Views/Rental/Reviews.cshtml | N/A | View tồn tại nhưng model KHÔNG clear. References VehicleReviewInputModel? |
| VIEW MODEL UNCLEAR | Views/Customer/Orders.cshtml | N/A | View tồn tại nhưng model type KHÔNG documented. |
| VIEW MODEL UNCLEAR | Views/Customer/Settings.cshtml | N/A | View tồn tại nhưng model undefined. |
| VIEW MODEL UNCLEAR | Views/Employee/ManageVehicles.cshtml | N/A | View tồn tại nhưng model NOT documented. |

---

## SUMMARY STATISTICS

| Loại Lỗi | Số Lượng | Mức Độ |
|----------|---------|--------|
| Missing Action | 10 | HIGH/MEDIUM |
| Missing Model | 7 | CRITICAL/HIGH |
| Table/Schema Undefined | 10 | MEDIUM |
| View Schema Undefined | 3 | MEDIUM |
| Duplicate/Conflicting | 4 | LOW/MEDIUM |
| Architecture Issues | 3 | MEDIUM/HIGH |
| Incomplete Implementation | 3 | MEDIUM |
| Missing Validation | 3 | MEDIUM |
| Code Duplication | 3 | MEDIUM |
| Inconsistent Handling | 3 | MEDIUM |
| Unverified Dependencies | 2 | MEDIUM |
| Security Issues | 1 | CRITICAL |
| **TOTAL** | **55+** | **MIXED** |

---

## RECOMMENDED FIX ORDER

### Phase 1 (CRITICAL - Do First)
1. Create missing Models: ContractFullViewModel, RentalDashboardViewModel, PendingContractViewModel
2. Create missing Models: BrandOptionViewModel, EmployeeDashboardViewModel, AdminVehicleOccupancyViewModel
3. Fix plaintext password security issue
4. Add missing Controller actions: Create, Update, ChangePassword, SendMessage, Search, CreateVehicle

### Phase 2 (HIGH - Do Soon)
5. Add missing CustomerController actions: AddFavorite, ToggleFavorite, AddReview, ShowContract
6. Consolidate CreateDraft GET/POST actions
7. Add [ValidateAntiForgeryToken] to ApproveContract
8. Remove duplicate views (Details/Detail, Dashboard/AdminDashboard)

### Phase 3 (MEDIUM - Nice to Have)
9. Implement LogActivityAsync
10. Standardize error handling
11. Extract duplicate code
12. Document database schema & stored procedures
13. Consider migration to EF Core
14. Verify SMTP email configuration

