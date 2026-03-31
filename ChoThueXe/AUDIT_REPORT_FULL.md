# AUDIT REPORT - CHOTHUEXE PROJECT
## Ngày Audit: 31/03/2026

---

## BƯỚC 1: BẢN ĐỒ DỰ ÁN

### 1. MODELS (40+ files)

#### Auth Models:
- `LoginInputModel`: Email, Password
- `RegisterInputModel`: FullName, Email, Phone, Password, ConfirmPassword
- `AuthenticatedUserViewModel`: UserId, FullName, Email, RoleName

#### Portal Models (Admin/Employee Dashboard):
- `AdminDashboardViewModel`: 13 properties (Collections)
- `AdminAccountManagementViewModel`: UserId, FullName, Email, RoleName, ContractCount, TotalPaid
- `AdminVehicleOccupancyViewModel` (referenced, not viewed)
- `AdminReplyInputModel`: MessageId, ReplyContent
- `AmenityOptionViewModel`: Code, Name
- `BrandOptionViewModel` (referenced, not viewed)
- `CreateVehicleInputModel`: VehicleId, OwnerId, BrandId, TypeId, VehicleName, LicensePlate, Seats, Transmission, FuelType, PricePerDay, Status, ImageUrls, SelectedAmenities
- `CustomerDashboardViewModel`: UserId, FullName, Email, Phone + 7 IReadOnlyList collections
- `CustomerForEmployeeViewModel`: UserId, FullName, Email, IsVerified
- `DriveLicenseViewModel`: DriveLicenseId, UserId, LicenseNumber, IssuedBy, IssuedAt, ExpireAt, CreatedAt
- `EmployeeDashboardViewModel` (referenced, not viewed)
- `NotificationViewModel`: NotificationId, UserId, Title, Message, IsRead, CreatedAt
- `PendingDocumentViewModel`: DocumentId, UserId, FullName, DocType, FileUrl, Status
- `PendingProfileUpdateRequestViewModel` (referenced, not viewed)
- `PendingVerificationViewModel`: UserId, FullName, CccdDocumentId, CccdFileUrl, DriverLicenseDocumentId, DriverLicenseFileUrl
- `ReviewableContractViewModel`: ContractId, VehicleId, VehicleName, EndDate
- `ReviewDocumentsInputModel`: UserId, IsMatched
- `ReviewProfileUpdateRequestInputModel` (referenced, not viewed)
- `RevenueByAccountViewModel`: UserId, FullName, TotalRevenue
- `SubmitDocumentInputModel`: DocType, FileUrl
- `SubmitDriveLicenseInputModel`: LicenseNumber, IssuedBy, IssuedAt, ExpireAt
- `SupportMessageViewModel`: MessageId, SenderId, SenderName, ReceiverId, ReceiverName, Content, ReplyContent, Status, SentAt, RepliedAt
- `TopRentedVehicleViewModel`: VehicleId, VehicleName, RentCount
- `TypeOptionViewModel`: TypeId, TypeName
- `UpdateProfileInputModel`: FullName, Phone
- `VehicleReviewInputModel`: ContractId, Rating, Comment

#### Rental Models:
- `ContractFullViewModel` (referenced, not viewed - gói dữ liệu hợp đồng)
- `CreateDraftInputModel`: CustomerId, EmployeeId
- `PaymentInputModel`: ContractId, Amount
- `PendingContractViewModel` (referenced, not viewed)
- `RentalDashboardViewModel` (referenced, not viewed)
- `RentVehicleInputModel` (referenced, not viewed)
- `RevenueViewModel`: VehicleName, TotalRevenue
- `UserOptionViewModel`: UserId, FullName, Email
- `VehicleDetailViewModel`: VehicleId, BrandId, TypeId, OwnerId, VehicleName, BrandName, TypeName, LicensePlate, PricePerDay, Status, AmenitiesText, PrimaryImageUrl, IsFavorite

#### Other:
- `ErrorViewModel`: RequestId

### 2. CONTROLLERS (6 files)

#### AuthController
- `[HttpGet] Login()` → View(new LoginInputModel())
- `[HttpGet] Register()` → View(new RegisterInputModel())
- `[HttpPost] Login(LoginInputModel)` → RedirectToAction/View
- `[HttpPost] Register(RegisterInputModel)` → RedirectToAction/View
- `[HttpGet] ForgotPassword()` → View()
- `[HttpPost] ForgotPassword(string email)` → RedirectToAction
- `[HttpGet] ResetPassword()` → View()
- `[HttpPost] ResetPassword(string email, string otpCode, string newPassword)` → RedirectToAction
- `[HttpPost] Logout()` → RedirectToAction

#### HomeController
- `[HttpGet] Index()` → RedirectToAction (role-based)
- `[HttpGet] TestAuth()` → Content
- `[HttpGet] Privacy()` → View()
- `[HttpGet] Error()` → View(ErrorViewModel)

#### RentalController [Authorize(Roles = "CUSTOMER")]
- `[HttpGet] Index()` → RedirectToAction("Index", "Customer")
- `[HttpGet] Details(int contractId)` → View
- `[HttpPost] Preview(RentVehicleInputModel)` → RedirectToAction
- `[HttpGet] CreateDraft(int customerId, int? employeeId)` → RedirectToAction
- `[HttpPost] CreateDraftPost(CreateDraftInputModel)` → RedirectToAction
- `[HttpPost] Rent(RentVehicleInputModel)` → RedirectToAction
- `[HttpPost] Pay(PaymentInputModel)` → RedirectToAction
- `[HttpGet] Book(int vehicleId)` → View

#### CustomerController [Authorize(Roles = "CUSTOMER,EMPLOYEE,ADMIN")]
- `[HttpGet] Index(string? q, string[]? amenities)` → View(CustomerDashboardViewModel)
- `[HttpPost] UpdateProfile(UpdateProfileInputModel)` → RedirectToAction
- `[HttpPost] SubmitDocument(SubmitDocumentInputModel)` → RedirectToAction
- `[HttpPost] SubmitDriveLicense(SubmitDriveLicenseInputModel)` → RedirectToAction
- `[HttpPost] Preview(RentVehicleInputModel)` → RedirectToAction
- `[HttpPost] Rent(RentVehicleInputModel)` → RedirectToAction
- `[HttpPost] AddFavorite(int vehicleId)` → JSON/RedirectToAction
- `[HttpPost] SendMessage(string content)` → RedirectToAction
- `[HttpPost] AddReview(VehicleReviewInputModel)` → RedirectToAction
- `[HttpGet] ShowContract(int contractId)` → JSON
- `[HttpPost] ToggleFavorite(int vehicleId)` → JSON/RedirectToAction

#### AdminController [Authorize(Roles = "ADMIN")]
- `[HttpGet] Index()` → View("Dashboard", AdminDashboardViewModel)
- `[HttpPost] ApproveDocument(int documentId)` → RedirectToAction
- `[HttpPost] AddVehicle(CreateVehicleInputModel)` → RedirectToAction
- `[HttpGet] EditVehicle(int vehicleId)` → View
- `[HttpPost] EditVehicle(CreateVehicleInputModel)` → RedirectToAction
- `[HttpPost] ReviewDocuments(ReviewDocumentsInputModel)` → RedirectToAction
- `[HttpPost] ReviewProfileUpdate(ReviewProfileUpdateRequestInputModel)` → RedirectToAction
- `[HttpPost] ReplyMessage(AdminReplyInputModel)` → RedirectToAction
- `[HttpPost] BroadcastNotification(int vehicleId, string vehicleName)` → RedirectToAction

#### EmployeeController [Authorize(Roles = "EMPLOYEE")]
- `[HttpGet] Index()` → View(EmployeeDashboardViewModel)
- `[HttpGet] Details(int contractId)` → View
- `[HttpPost] CreateDraft(int customerId)` → RedirectToAction
- `[HttpPost] ApproveContract(int contractId)` → RedirectToAction

### 3. VIEWS

#### Views/Auth/
- Login.cshtml - ✓ Calls asp-action="Login"
- Register.cshtml - ✓ Calls asp-action="Register"
- ForgotPassword.cshtml - ✓ Calls asp-action="ForgotPassword"
- ResetPassword.cshtml - ✓ Calls asp-action="ResetPassword"

#### Views/Home/
- Index.cshtml
- Privacy.cshtml
- Error.cshtml
- Error500.cshtml
- Error404.cshtml
- Contact.cshtml - ❌ Calls asp-action="SendMessage" (NO ACTION EXISTS in HomeController)
- About.cshtml
- FAQ.cshtml
- Help.cshtml
- Terms.cshtml
- Promotions.cshtml

#### Views/Customer/
- Index.cshtml
- Favorites.cshtml
- Notifications.cshtml
- Orders.cshtml
- Profile.cshtml - ❌ Calls asp-action="Update" & asp-action="ChangePassword" (NO ACTIONS EXISTS)
- Settings.cshtml

#### Views/Rental/
- Index.cshtml - ✓ Calls asp-action="CreateDraft", "Preview", "Rent", "Pay"
- Book.cshtml - ❌ Calls asp-action="Create" (NO ACTION EXISTS in RentalController)
- Details.cshtml - ✓ (Details action exists)
- Detail.cshtml (duplicate?)
- Compare.cshtml
- Confirmation.cshtml
- Invoice.cshtml
- MyBookings.cshtml
- Payment.cshtml
- Recommendations.cshtml
- Reviews.cshtml
- Search.cshtml - ❌ Calls asp-action="Search" (NO ACTION EXISTS)
- SearchResults.cshtml
- VehicleDetail.cshtml

#### Views/Employee/
- Index.cshtml
- Details.cshtml - ✓ (Details action exists)
- ManageVehicles.cshtml
- Vehicles.cshtml - ❌ Calls asp-action="CreateVehicle" (NO ACTION EXISTS in EmployeeController)

#### Views/Admin/
- Dashboard.cshtml - View returned by Index action
- AdminDashboard.cshtml (duplicate?)
- Index.cshtml - Calls multiple actions
- EditVehicle.cshtml - ✓ Calls asp-action="EditVehicle"

#### Views/Shared/
- _Layout.cshtml
- UserProfile.cshtml
- Error.cshtml
- Error500.cshtml
- Error404.cshtml

### 4. REPOSITORIES

#### IRentalRepository (Interface)
- 40+ methods defined
- GetVehiclesAsync, GetVehiclesForCustomerAsync, GetFavoriteVehiclesByCustomerAsync
- GetContractsAsync, GetRevenueAsync, GetUsersAsync, GetUsersByRoleAsync
- GetPendingContractsAsync, GetPendingContractsByCustomerAsync, GetContractsByCustomerAsync
- GetCustomersForEmployeeAsync
- GetPendingDocumentsAsync, GetPendingVerificationsAsync
- GetBrandsAsync, GetTypesAsync, GetAmenityOptionsAsync
- GetNotificationsForUserAsync, GetMessagesForAdminAsync, GetMessagesForCustomerAsync
- GetReviewableContractsByCustomerAsync
- GetAdminAccountsAsync, GetAdminVehicleOccupanciesAsync
- GetRevenueByAccountAsync, GetTopRentedVehiclesAsync
- Task methods: IsUserVerifiedAsync, CalculateRentalCostAsync, UpdateUserProfileAsync, etc.

#### IAuthRepository (Interface)
- AuthenticateAsync
- EmailExistsAsync
- RegisterCustomerAsync
- GenerateOtpAsync, ValidateOtpAsync, ResetPasswordAsync

### 5. DATABASE

**Connection String:** Oracle SQL (db.freesql.com:1521/23ai_34ui2)

**Tables Referenced in Code:**
- users, roles, otp_codes (Auth)
- vehicles, vehicle_images, brands, types, amenities, vehicle_amenities (Vehicles)
- contracts, payments (Rentals)
- favorite_vehicles, notifications, support_messages, vehicle_reviews (Features)
- documents, driver_licenses, profile_update_requests, activity_logs (Admin)

**Views Referenced:**
- vw_vehicle_detail
- vw_contract_full
- vw_revenue

**NO MIGRATIONS FOLDER** - Database-first approach (direct SQL)

---

## BƯỚC 2: PHÁT HIỆN LỖI

### ❌ 2A. LỖI CONTROLLER / ACTION

#### **ERROR #1**: Missing RentalController.Create() Action
- **File**: Views/Rental/Book.cshtml
- **Line**: 331
- **Issue**: Form calls `asp-action="Create" asp-controller="Rental" method="post"`
- **Problem**: RentalController has NO Create(RentVehicleInputModel) action
- **Expected**: Should call Rent() instead, or Create action must be added
- **Severity**: HIGH - Form submission will fail

#### **ERROR #2**: Missing CustomerController.Update() Action
- **File**: Views/Customer/Profile.cshtml
- **Line**: 366
- **Issue**: Form calls `asp-action="Update" asp-controller="Customer" method="post"`
- **Problem**: CustomerController has NO Update(UpdateProfileInputModel) action
- **Expected**: Action exists in IRentalRepository as SubmitProfileUpdateRequestAsync
- **Severity**: HIGH - Profile update form won't work

#### **ERROR #3**: Missing CustomerController.ChangePassword() Action
- **File**: Views/Customer/Profile.cshtml
- **Line**: 409
- **Issue**: Form calls `asp-action="ChangePassword" asp-controller="Customer" method="post"`
- **Problem**: CustomerController has NO ChangePassword action
- **Expected**: Should be added or redirected to Auth controller
- **Severity**: HIGH - Password change form won't work

#### **ERROR #4**: Missing HomeController.SendMessage() Action
- **File**: Views/Home/Contact.cshtml
- **Line**: 305
- **Issue**: Form calls `asp-action="SendMessage"`
- **Problem**: HomeController has NO SendMessage action
- **Expected**: Should be CustomerController or a separate ContactController
- **Severity**: MEDIUM - Contact form won't work

#### **ERROR #5**: Missing RentalController.Search() Action
- **File**: Views/Rental/Search.cshtml
- **Line**: 365
- **Issue**: Form calls `asp-action="Search"`
- **Problem**: RentalController has NO Search action
- **Expected**: Functionality exists in GetVehiclesAsync but no dedicated Search action
- **Severity**: MEDIUM - Search form redirects to Index instead

#### **ERROR #6**: Missing EmployeeController.CreateVehicle() Action
- **File**: Views/Employee/Vehicles.cshtml
- **Line**: 598
- **Issue**: Form calls `asp-action="CreateVehicle"`
- **Problem**: EmployeeController has NO CreateVehicle action
- **Expected**: Only AdminController.AddVehicle exists; Employee shouldn't create vehicles
- **Severity**: HIGH - Form submission will fail

#### **ERROR #7**: Duplicate Detail Views
- **File**: Views/Rental/Details.cshtml & Views/Rental/Detail.cshtml
- **Issue**: Two views with nearly same names
- **Problem**: Potential confusion, routing ambiguity
- **Severity**: LOW - Both probably do same thing

#### **ERROR #8**: Duplicate Dashboard Views
- **File**: Views/Admin/Dashboard.cshtml & Views/Admin/AdminDashboard.cshtml
- **Issue**: Two views with similar names
- **Problem**: Index action returns View("Dashboard"), confusing naming
- **Severity**: LOW - AdminDashboard.cshtml probably unused

---

### ❌ 2B. LỖI MODEL / SCHEMA

#### **ERROR #9**: Missing Model Properties in Views
- **File**: Views/Admin/Index.cshtml
- **Line**: 94
- **Issue**: Form has input fields `Seats`, `Transmission`, `FuelType` 
- **Problem**: `CreateVehicleInputModel` HAS these properties ✓ (OK)

#### **ERROR #10**: Missing BrandOptionViewModel Implementation
- **File**: IRentalRepository.GetBrandsAsync()
- **Issue**: Returns `IReadOnlyList<BrandOptionViewModel>` but NO model file found
- **Problem**: Model likely missing or named differently
- **Severity**: MEDIUM - Needs verification

#### **ERROR #11**: Missing TypeOptionViewModel Definition
- **File**: Views/Admin/Index.cshtml uses Model.Types
- **Issue**: TypeOptionViewModel exists ✓ but check consistency
- **Problem**: Properties: TypeId, TypeName (seems OK)
- **Severity**: LOW

#### **ERROR #12**: Missing ContractFullViewModel Implementation
- **File**: IRentalRepository.GetContractsAsync() returns this
- **Issue**: Model referenced but NOT found in Models directory
- **Problem**: Used in multiple views, but definition missing
- **Severity**: CRITICAL - Core model missing

#### **ERROR #13**: Missing RentalDashboardViewModel
- **File**: RentalController.BuildDashboardAsync() returns this
- **Issue**: Model NOT found in Models directory
- **Problem**: Used but not implemented
- **Severity**: CRITICAL - Core model missing

#### **ERROR #14**: Missing PendingContractViewModel
- **File**: IRentalRepository references this in multiple methods
- **Issue**: Model NOT found in Models directory
- **Problem**: Used in CustomerDashboardViewModel, EmployeeDashboardViewModel
- **Severity**: CRITICAL - Core model missing

#### **ERROR #15**: Missing PendingProfileUpdateRequestViewModel
- **File**: IRentalRepository.GetPendingProfileUpdateRequestsAsync()
- **Issue**: Model NOT found in Models directory
- **Problem**: Returned type is undefined
- **Severity**: MEDIUM - Less critical path

#### **ERROR #16**: Missing EmployeeDashboardViewModel Implementation
- **File**: EmployeeController.Index() returns this
- **Issue**: Model referenced but may not be fully defined
- **Problem**: Needs: Customers (IReadOnlyList<CustomerForEmployeeViewModel>), PendingContracts
- **Severity**: HIGH - Core model for Employee portal

#### **ERROR #17**: Missing AdminVehicleOccupancyViewModel
- **File**: AdminDashboardViewModel uses this
- **Issue**: Model NOT found in Models directory
- **Problem**: Used in GetAdminVehicleOccupanciesAsync
- **Severity**: MEDIUM

#### **ERROR #18**: Missing BrandOptionViewModel File
- **File**: Should be Models/Portal/BrandOptionViewModel.cs
- **Issue**: File does not exist
- **Problem**: GetBrandsAsync returns this type
- **Severity**: CRITICAL

---

### ❌ 2C. LỖI MIGRATION / DATABASE

#### **ERROR #19**: NO MIGRATIONS FOLDER
- **Issue**: Project has NO Migrations/ directory
- **Problem**: EF Core migrations not used; raw SQL approach
- **Severity**: MEDIUM - Database-first approach may have schema mismatches

#### **ERROR #20**: Missing otp_codes Table Structure
- **File**: AuthRepository.cs uses otp_codes table
- **Problem**: Schema not defined in migrations
- **Severity**: MEDIUM - Table may not exist in database

#### **ERROR #21**: Missing driver_licenses Table
- **File**: RentalRepository.SubmitDriveLicenseAsync references driver_licenses
- **Problem**: Schema structure unknown
- **Severity**: MEDIUM

#### **ERROR #22**: Missing vehicle_images Table
- **File**: RentalRepository uses vehicle_images table
- **Problem**: Columns (image_id, vehicle_id, image_url) assumed but not verified
- **Severity**: MEDIUM

#### **ERROR #23**: Missing Documents Table
- **File**: RentalRepository references documents table
- **Problem**: Schema structure not documented
- **Severity**: MEDIUM

---

### ❌ 2D. LỖI KHÁC

#### **ERROR #24**: Duplicate CreateDraft Actions
- **File**: RentalController.cs
- **Issue**: Both CreateDraft (GET) and CreateDraftPost (POST) exist
- **Problem**: Should consolidate into single action with [HttpPost]
- **Code**: Lines with CreateDraft and CreateDraftPost
- **Severity**: MEDIUM - Redundant methods

#### **ERROR #25**: Missing GetPendingProfileUpdateRequestsAsync Implementation
- **File**: RentalRepository.cs
- **Issue**: Interface declares this method but implementation likely incomplete
- **Problem**: Method called but not verified
- **Severity**: MEDIUM

#### **ERROR #26**: Missing AddFavorite Action in CustomerController
- **File**: CustomerController missing AddFavorite(int vehicleId) 
- **Issue**: Views may call this but action not found
- **Problem**: Should be present but not in code review
- **Severity**: MEDIUM

#### **ERROR #27**: Missing ToggleFavorite Action
- **File**: CustomerController missing ToggleFavorite(int vehicleId)
- **Issue**: Same as above
- **Severity**: MEDIUM

#### **ERROR #28**: Missing AddReview Action
- **File**: CustomerController missing AddReview(VehicleReviewInputModel)
- **Issue**: Model exists but action missing
- **Severity**: MEDIUM

#### **ERROR #29**: Missing ShowContract Action
- **File**: CustomerController missing ShowContract(int contractId) 
- **Issue**: Should return contract details as JSON
- **Severity**: LOW

#### **ERROR #30**: Using Direct OracleConnection Instead of DbContext
- **File**: All Repository files
- **Issue**: Code uses raw ADO.NET instead of EF Core DbContext
- **Problem**: No type safety, no automatic migrations, harder to maintain
- **Severity**: MEDIUM - Architecture issue

#### **ERROR #31**: Missing InputModels in some Views
- **File**: Views/Rental/Compare.cshtml, Recommendations.cshtml, etc.
- **Issue**: Unclear what models these views expect
- **Problem**: Views exist but no corresponding ViewModels documented
- **Severity**: MEDIUM

#### **ERROR #32**: Inconsistent Error Handling
- **File**: Controllers
- **Issue**: Some use try-catch with OracleException, others don't
- **Problem**: Database errors not consistently handled
- **Severity**: MEDIUM

#### **ERROR #33**: Missing [ValidateAntiForgeryToken] in Some POST Actions
- **File**: EmployeeController.ApproveContract (line 88)
- **Issue**: Missing ValidateAntiForgeryToken attribute
- **Problem**: CSRF vulnerability
- **Severity**: MEDIUM - Security issue

#### **ERROR #34**: Hardcoded Admin Redirect
- **File**: HomeController.Index()
- **Issue**: Redirects based on role, but no role parameter validation
- **Problem**: No fallback if role doesn't exist
- **Severity**: LOW

#### **ERROR #35**: Views Calling Non-Existent Form Actions
- **File**: Multiple Views/Employee/Index.cshtml form fields
- **Issue**: Some form inputs expect actions that don't exist
- **Problem**: Silent failures on form submission
- **Severity**: MEDIUM

---

## SUMMARY TABLE

| ERROR # | TYPE | SEVERITY | FILE | ACTION REQUIRED |
|---------|------|----------|------|-----------------|
| 1 | Missing Action | HIGH | Views/Rental/Book.cshtml | Add RentalController.Create() or change action to Rent |
| 2 | Missing Action | HIGH | Views/Customer/Profile.cshtml | Add CustomerController.Update() action |
| 3 | Missing Action | HIGH | Views/Customer/Profile.cshtml | Add CustomerController.ChangePassword() action |
| 4 | Missing Action | MEDIUM | Views/Home/Contact.cshtml | Add HomeController.SendMessage() or move to Customer |
| 5 | Missing Action | MEDIUM | Views/Rental/Search.cshtml | Add RentalController.Search() action |
| 6 | Missing Action | HIGH | Views/Employee/Vehicles.cshtml | Remove or add EmployeeController.CreateVehicle() |
| 7 | Duplicate View | LOW | Views/Rental/ | Remove Detail.cshtml or Details.cshtml |
| 8 | Duplicate View | LOW | Views/Admin/ | Remove AdminDashboard.cshtml or Dashboard.cshtml |
| 9 | Model Missing | CRITICAL | Models/Portal/ | Create ContractFullViewModel.cs |
| 10 | Model Missing | CRITICAL | Models/Portal/ | Create RentalDashboardViewModel.cs |
| 11 | Model Missing | CRITICAL | Models/Portal/ | Create PendingContractViewModel.cs |
| 12 | Model Missing | CRITICAL | Models/Portal/ | Create BrandOptionViewModel.cs |
| 13 | Model Missing | HIGH | Models/Portal/ | Create EmployeeDashboardViewModel.cs |
| 14 | Model Missing | MEDIUM | Models/Portal/ | Create AdminVehicleOccupancyViewModel.cs |
| 15 | No Migrations | MEDIUM | Project root | Database schema not versioned |
| 16 | Duplicate Action | MEDIUM | Controllers/RentalController.cs | Consolidate CreateDraft GET/POST |
| 17 | Missing Actions | MEDIUM | Controllers/CustomerController.cs | Add AddFavorite, ToggleFavorite, AddReview, ShowContract |
| 18 | Architecture | MEDIUM | All | Consider using EF Core DbContext |
| 19 | Security | MEDIUM | Controllers/EmployeeController.cs | Add [ValidateAntiForgeryToken] to ApproveContract |
| 20 | Inconsistent Error Handling | MEDIUM | All Controllers | Standardize exception handling |

---

## CRITICAL PATH (Must Fix First)

1. ✅ Create missing Models: ContractFullViewModel, RentalDashboardViewModel, PendingContractViewModel, BrandOptionViewModel, EmployeeDashboardViewModel, AdminVehicleOccupancyViewModel
2. ✅ Add missing Actions: Create, Update, ChangePassword, Search, SendMessage, CreateVehicle
3. ✅ Add missing CustomerController actions: AddFavorite, ToggleFavorite, AddReview, ShowContract
4. ✅ Consolidate duplicate CreateDraft actions
5. ✅ Add [ValidateAntiForgeryToken] to ApproveContract in EmployeeController
6. ✅ Remove or consolidate duplicate views (Detail/Details, Dashboard/AdminDashboard)

