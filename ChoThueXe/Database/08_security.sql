-- ============================================================================
-- FILE: 08_security.sql
-- PURPOSE: Thiết lập bảo mật và phân quyền trên hệ thống cho thuê xe (ChoThueXe)
-- DATABASE: Oracle 19c
-- AUTHOR: Database Team / DBA
-- CREATED: 2026-04-03
-- DESCRIPTION:
--   Script này triển khai chiến lược bảo mật dựa trên nguyên tắc Least Privilege:
--   - admin_role: Quyền quản trị toàn bộ
--   - user_role: Quyền tối thiểu cho Customer/Employee (xem, tạo hợp đồng)
--
--   PHẦN 1: Tạo 2 role chính
--   PHẦN 2: Cấp quyền cho user_role (tính năng read-only + create transactions)
--   PHẦN 3: Cấp quyền cho admin_role (quản trị toàn bộ)
--   PHẦN 4: Gán role cho user cụ thể (tuỳ chọn)
--   PHẦN 5: Kiểm tra quyền được cấp
-- ============================================================================

-- ============================================================================
-- PHẦN 1: TẠO CÁC ROLE
-- ============================================================================
-- Mục đích: Định nghĩa 2 vai trò chính trong hệ thống
-- admin_role: Quản trị viên hệ thống, có toàn bộ quyền
-- user_role: Khách hàng/Nhân viên, chỉ có quyền tối thiểu để hoạt động

PROMPT;
PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ PHẦN 1: TẠO CÁC ROLE                                              ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

-- Tạo role cho admin/manager
-- Comment: Vai trò quản trị viên - người dùng này sẽ quản lý toàn hệ thống
CREATE ROLE admin_role;
PROMPT '[SUCCESS] Tạo role: admin_role';

-- Tạo role cho khách hàng/nhân viên
-- Comment: Vai trò người dùng bình thường - có quyền nhất định để tương tác
CREATE ROLE user_role;
PROMPT '[SUCCESS] Tạo role: user_role';

PROMPT;

-- ============================================================================
-- PHẦN 2: PHÂN QUYỀN CHO USER_ROLE (Khách hàng / Nhân viên)
-- ============================================================================
-- Nguyên tắc: Least Privilege (Quyền tối thiểu)
--   - Chỉ cấp quyền CẦN THIẾT để hoàn thành công việc
--   - KHÔNG cấp quyền DELETE, UPDATE trên dữ liệu quan trọng (contracts)
--   - KHÔNG cho phép xem bảng users trực tiếp (tránh lộ mật khẩu)
--   - Cho phép xem thông qua VIEW an toàn (vw_user_verification)

PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ PHẦN 2: PHÂN QUYỀN USER_ROLE - KHÁCH HÀNG / NHÂN VIÊN            ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

-- ════════════════════════════════════════════════════════════════════════════
-- 2.1: SELECT trên các bảng chỉ xem (vehicles, reviews)
-- ════════════════════════════════════════════════════════════════════════════

-- Mục đích: Cho phép xem danh sách xe khả dụng
-- Lý do: Khách hàng cần tìm kiếm xe để thuê
GRANT SELECT ON vehicles TO user_role;
PROMPT '[GRANT] SELECT ON vehicles → user_role';
PROMPT '  → Cho phép xem danh sách xe, giá, loại xe';

-- Mục đích: Cho phép xem bình luận/đánh giá xe
-- Lý do: Khách hàng cần đọc review để quyết định chọn xe
GRANT SELECT ON reviews TO user_role;
PROMPT '[GRANT] SELECT ON reviews → user_role';
PROMPT '  → Cho phép xem đánh giá từ những khách hàng khác';

PROMPT;

-- ════════════════════════════════════════════════════════════════════════════
-- 2.2: SELECT trên các VIEW (đã được filter an toàn)
-- ════════════════════════════════════════════════════════════════════════════

-- Mục đích: Cho phép xem thông tin xe chi tiết (brand, type, amenities)
-- Lý do: VIEW được định nghĩa rõ, chỉ show các column an toàn
-- Khác biệt: Không cho SELECT trên bảng brands/types trực tiếp,
--            mà cho xem thông qua VIEW để kiểm soát cột
GRANT SELECT ON vw_vehicle_detail TO user_role;
PROMPT '[GRANT] SELECT ON vw_vehicle_detail → user_role';
PROMPT '  → Xem xe kèm tên hãng, loại qua VIEW an toàn';

-- Mục đích: Cho phép xem toàn bộ thông tin hợp đồng của mình
-- Lý do: Khách hàng cần xem lịch sử thuê xe, hóa đơn
GRANT SELECT ON vw_contract_full TO user_role;
PROMPT '[GRANT] SELECT ON vw_contract_full → user_role';
PROMPT '  → Xem chi tiết hợp đồng đã tạo';

-- Mục đích: Cho phép kiểm tra trạng thái xác minh giấy tờ của user
-- Lý do: Khách hàng muốn biết giấy tờ đã được duyệt hay chưa
GRANT SELECT ON vw_user_verification TO user_role;
PROMPT '[GRANT] SELECT ON vw_user_verification → user_role';
PROMPT '  → Xem trạng thái xác minh giấy tờ (CCCD, bằng lái)';

-- Mục đích: Cho phép xem doanh thu (cho admin/manager view)
-- Lý do: Nhân viên quản lý xe cần biết xe nào sinh lợi
GRANT SELECT ON vw_revenue TO user_role;
PROMPT '[GRANT] SELECT ON vw_revenue → user_role';
PROMPT '  → Xem doanh thu theo xe (chỉ hợp đồng đã hoàn thành)';

PROMPT;

-- ════════════════════════════════════════════════════════════════════════════
-- 2.3: SELECT + INSERT trên các bảng giao dịch
-- ════════════════════════════════════════════════════════════════════════════

-- Mục đích: Cho phép tạo hợp đồng mới
-- Lý do: Khách hàng/nhân viên cần book xe, tạo hợp đồng
-- Bảo mật: KHÔNG cấp UPDATE, DELETE → ngăn chặn thay đổi/xóa hợp đồng
--         (chỉ admin mới được sửa/xóa hợp đồng)
GRANT SELECT, INSERT ON contracts TO user_role;
PROMPT '[GRANT] SELECT, INSERT ON contracts → user_role';
PROMPT '  → Xem hợp đồng + Tạo hợp đồng mới';
PROMPT '  → KHÔNG có quyền UPDATE/DELETE (admin-only)';

-- Mục đích: Cho phép tạo chi tiết hợp đồng (cặp xe - ngày thuê)
-- Lý do: Mỗi hợp đồng có thể có nhiều chi tiết (thuê 2 xe cùng lúc)
GRANT SELECT, INSERT ON contract_details TO user_role;
PROMPT '[GRANT] SELECT, INSERT ON contract_details → user_role';
PROMPT '  → Xem + Tạo chi tiết hợp đồng';

-- Mục đích: Cho phép ghi nhận thanh toán
-- Lý do: Khách hàng/nhân viên cần tạo record thanh toán
GRANT SELECT, INSERT ON payments TO user_role;
PROMPT '[GRANT] SELECT, INSERT ON payments → user_role';
PROMPT '  → Xem + Ghi nhận thanh toán';

-- Mục đích: Cho phép gửi tin nhắn tới admin
-- Lý do: Khách hàng cần liên hệ admin về vấn đề
GRANT SELECT, INSERT ON conversations TO user_role;
PROMPT '[GRANT] SELECT, INSERT ON conversations → user_role';
PROMPT '  → Xem + Tạo cuộc hội thoại mới';

-- Mục đích: Cho phép gửi tin nhắn
-- Lý do: Khách hàng gửi tin, admin trả lời
GRANT SELECT, INSERT ON messages TO user_role;
PROMPT '[GRANT] SELECT, INSERT ON messages → user_role';
PROMPT '  → Xem + Gửi tin nhắn';

PROMPT;

-- ════════════════════════════════════════════════════════════════════════════
-- 2.4: KHÔNG cấp quyền trên các bảng nhạy cảm
-- ════════════════════════════════════════════════════════════════════════════

PROMPT '[INFO] user_role KHÔNG có quyền trên:';
PROMPT '  ✗ users (bảng nhạy cảm, chứa password hash)';
PROMPT '  ✗ user_documents (bảng xác minh, chứa dữ liệu riêng tư)';
PROMPT '  ✗ otp_codes (mã OTP - riêng tư)';
PROMPT '  ✗ brands, vehicle_types (dùng VIEW thay vì trực tiếp)';
PROMPT;

-- ============================================================================
-- PHẦN 3: PHÂN QUYỀN CHO ADMIN_ROLE (Quản trị viên)
-- ============================================================================
-- Mục đích: Cấp quyền toàn bộ cho admin/DBA
-- Lý do: Admin cần quản lý toàn hệ thống, sửa/xóa dữ liệu khi cần

PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ PHẦN 3: PHÂN QUYỀN ADMIN_ROLE - QUẢN TRỊ VIÊN                    ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng users
-- Mục đích: Admin quản lý tài khoản user (tạo, chỉnh sửa, khóa)
GRANT SELECT, INSERT, UPDATE, DELETE ON users TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON users → admin_role';
PROMPT '  → Quản lý tài khoản user (tạo, cấp quyền, khóa)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng vehicles
-- Mục đích: Admin quản lý danh sách xe (thêm, xóa, bảo dưỡng)
GRANT SELECT, INSERT, UPDATE, DELETE ON vehicles TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON vehicles → admin_role';
PROMPT '  → Quản lý xe (thêm/xóa/sửa thông tin)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng contracts
-- Mục đích: Admin xử lý hợp đồng (duyệt, hủy, sửa ngày thuê)
GRANT SELECT, INSERT, UPDATE, DELETE ON contracts TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON contracts → admin_role';
PROMPT '  → Quản lý hợp đồng (duyệt, hủy, xử lý tranh chấp)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng contract_details
-- Mục đích: Admin chỉnh sửa chi tiết hợp đồng (ngày, giá tiền)
GRANT SELECT, INSERT, UPDATE, DELETE ON contract_details TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON contract_details → admin_role';
PROMPT '  → Sửa chi tiết hợp đồng (ngày, giá)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng payments
-- Mục đích: Admin xử lý thanh toán (ghi nhận, sửa lỗi, hoàn lại)
GRANT SELECT, INSERT, UPDATE, DELETE ON payments TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON payments → admin_role';
PROMPT '  → Quản lý thanh toán (ghi nhận, xử lý tranh chấp)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng conversations
-- Mục đích: Admin xem/xóa cuộc hội thoại
GRANT SELECT, INSERT, UPDATE, DELETE ON conversations TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON conversations → admin_role';
PROMPT '  → Quản lý hội thoại (xem, xóa nội dung spam)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng messages
-- Mục đích: Admin xem/xóa tin nhắn
GRANT SELECT, INSERT, UPDATE, DELETE ON messages TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON messages → admin_role';
PROMPT '  → Quản lý tin nhắn (xem, xóa)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng user_documents
-- Mục đích: Admin duyệt giấy tờ, xóa giấy tờ không hợp lệ
GRANT SELECT, INSERT, UPDATE, DELETE ON user_documents TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON user_documents → admin_role';
PROMPT '  → Duyệt/từ chối giấy tờ xác minh (CCCD, bằng lái)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng reviews
-- Mục đích: Admin xóa review spam, xấu
GRANT SELECT, INSERT, UPDATE, DELETE ON reviews TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON reviews → admin_role';
PROMPT '  → Kiểm duyệt review (xóa spam, review xấu)';

-- Cấp SELECT, INSERT, UPDATE, DELETE trên bảng otp_codes
-- Mục đích: Admin quản lý OTP (xem log, xóa OTP cũ)
GRANT SELECT, INSERT, UPDATE, DELETE ON otp_codes TO admin_role;
PROMPT '[GRANT] SELECT, INSERT, UPDATE, DELETE ON otp_codes → admin_role';
PROMPT '  → Quản lý OTP (kiểm tra log, xóa OTP expired)';

-- Cấp SELECT trên tất cả VIEW cho admin
-- Mục đích: Admin xem tất cả báo cáo, thống kê
GRANT SELECT ON vw_vehicle_detail TO admin_role;
GRANT SELECT ON vw_contract_full TO admin_role;
GRANT SELECT ON vw_user_verification TO admin_role;
GRANT SELECT ON vw_revenue TO admin_role;
PROMPT '[GRANT] SELECT ON ALL VIEWS → admin_role';
PROMPT '  → Xem tất cả báo cáo, thống kê';

PROMPT;

-- ============================================================================
-- PHẦN 4: GÁN ROLE CHO USER CỤ THỂ (TUỲ CHỌN)
-- ============================================================================
-- Lưu ý: Script này chỉ tạo role và cấp quyền
-- Để gán role cho user cụ thể, dùng:
--   GRANT admin_role TO username;
--   GRANT user_role TO username;

PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ PHẦN 4: CÁCH GÁN ROLE CHO USER (VÍ DỤ)                           ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

PROMPT 'Để gán role cho user cụ thể, chạy lệnh (admin chạy):';
PROMPT '  GRANT admin_role TO <admin_user>;     -- cho admin user';
PROMPT '  GRANT user_role TO <customer_user>;   -- cho khách hàng/nhân viên';
PROMPT;

-- Ví dụ (nếu bạn chạy sẽ uncomment):
-- GRANT admin_role TO app_admin;
-- GRANT user_role TO customer_john;
-- GRANT user_role TO employee_jane;

PROMPT;

-- ============================================================================
-- PHẦN 5: KIỂM TRA QUYỀN ĐÃ CẤP
-- ============================================================================

PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ PHẦN 5: KIỂM TRA QUYỀN ĐÃ CẤP                                     ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

PROMPT 'Quyền của admin_role:';
SELECT grantee, privilege 
FROM role_sys_privs 
WHERE role = 'ADMIN_ROLE'
ORDER BY privilege;

PROMPT;
PROMPT 'Quyền của user_role:';
SELECT grantee, privilege 
FROM role_sys_privs 
WHERE role = 'USER_ROLE'
ORDER BY privilege;

PROMPT;
PROMPT 'Bảng mà admin_role có quyền:';
SELECT role, table_name, select_priv, insert_priv, update_priv, delete_priv
FROM role_tab_privs
WHERE role = 'ADMIN_ROLE'
ORDER BY table_name;

PROMPT;
PROMPT 'Bảng mà user_role có quyền:';
SELECT role, table_name, select_priv, insert_priv, update_priv, delete_priv
FROM role_tab_privs
WHERE role = 'USER_ROLE'
ORDER BY table_name;

PROMPT;

-- ============================================================================
-- GIẢI THÍCH KHÁI NIỆM BẢO MẬT
-- ============================================================================

/*
╔════════════════════════════════════════════════════════════════════════════╗
║                   NGUYÊN TẮC LEAST PRIVILEGE (PoLP)                       ║
║         Principle of Least Privilege - Phân quyền tối thiểu               ║
╚════════════════════════════════════════════════════════════════════════════╝

1. LEAST PRIVILEGE LÀ GÌ?
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   "Mỗi user/role chỉ được cấp quyền CẦN THIẾT NHẤT để hoàn thành công việc,
    không hơn không kém."
   
   ❌ SAI: GRANT ALL PRIVILEGES ON ALL TABLES TO user_role;
   ✓ ĐÚNG: GRANT SELECT, INSERT ON contracts TO user_role;
            (Chỉ cấp đủ để xem & tạo hợp đồng)
   
   Lợi ích:
   • Tăng bảo mật: Giảm rủi ro lộ lọt dữ liệu
   • Giảm thiệt hại: Nếu account bị hack, attacker chỉ có quyền tối thiểu
   • Kiểm soát tốt: Dễ audit, biết ai làm gì


2. ÁP DỤNG TRONG SCRIPT 08_SECURITY.SQL
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   a) user_role chỉ có SELECT, INSERT (không UPDATE/DELETE)
      ┌───────────────────────────────────────────────────────────────┐
      │ ✓ SELECT ON contracts  → Xem hợp đồng của mình               │
      │ ✓ INSERT ON contracts  → Tạo hợp đồng mới                    │
      │ ✗ UPDATE ON contracts  → KHÔNG sửa hợp đồng (admin làm)     │
      │ ✗ DELETE ON contracts  → KHÔNG xóa hợp đồng (admin làm)     │
      │                                                               │
      │ Tại sao?                                                      │
      │ - Khách hàng không thể tự sửa/xóa hợp đồng (anti-fraud)     │
      │ - Chỉ admin mới được sửa giá, xóa hợp đồng sai              │
      │ - Đảm bảo tính toàn vẹn dữ liệu, không có người dùng bất lực │
      └───────────────────────────────────────────────────────────────┘
   
   b) user_role KHÔNG có quyền trên bảng users
      ┌───────────────────────────────────────────────────────────────┐
      │ ✗ SELECT ON users  → KHÔNG xem danh sách user/password       │
      │ ✗ UPDATE ON users  → KHÔNG sửa thông tin user khác          │
      │                                                               │
      │ Thay vào đó:                                                  │
      │ ✓ SELECT ON vw_user_verification                             │
      │   → Xem trạng thái xác minh của mình qua VIEW an toàn        │
      │                                                               │
      │ Tại sao?                                                      │
      │ - Bảng users chứa password hash → rủi ro bảo mật cao        │
      │ - USER A không cần biết email/SĐT của USER B                │
      │ - VIEW được filter an toàn: chỉ show verification_status    │
      └───────────────────────────────────────────────────────────────┘


3. TẠI SAO user_role KHÔNG CÓ QUYỀN DELETE CONTRACTS?
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   Kịch bản 1 (SAI - nếu cấp DELETE):
   ┌─────────────────────────────────────────────────────────────┐
   │ Khách hàng John tạo hợp đồng C001 để thuê xe               │
   │ John thấy giá quá cao → DELETE hợp đồng C001               │
   │ → Hệ thống không có bản ghi: "John đã thuê xe vào 2026-04" │
   │ → Xe bị ghi nhận "không có người thuê" (lỗi data)          │
   │ → Admin không biết xe đó đã được book, cho khách khác       │
   │ → DOUBLE BOOKING (overbooking) → scandal!                  │
   └─────────────────────────────────────────────────────────────┘
   
   Kịch bản 2 (ĐÚNG - không cấp DELETE):
   ┌─────────────────────────────────────────────────────────────┐
   │ John tạo hợp đồng C001                                      │
   │ John muốn hủy: gọi admin hoặc nhấn nút "Cancel" trên app   │
   │ App gọi stored procedure sp_cancel_contract() (only admin)  │
   │ → Hợp đồng status = 'CANCELLED' (soft delete, không xóa)   │
   │ → Audit log: "C001 hủy bởi John lúc 2026-04-03 14:30"     │
   │ → Admin kiểm tra lý do hủy, có thể hoàn lại tiền           │
   │ → Dữ liệu lịch sử toàn vẹn ✓                              │
   └─────────────────────────────────────────────────────────────┘
   
   Kết luận: DELETE không cấp = Ngăn chặn data corruption + audit trail


4. TẠI SAO user_role KHÔNG NHÌN THẤY BẢNG USERS TRỰC TIẾP?
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   Bảng users chứa dữ liệu nhạy cảm:
   ┌────────────────────────────────────────────────────────────┐
   │ user_id | full_name | email | phone | password_hash |     │
   ├────────────────────────────────────────────────────────────┤
   │ 11      | John Doe  | j@... | 090.. | 8a3f4a9b2d... | ← │
   │ 12      | Jane Doe  | z@... | 091.. | 5c7e1b4c9a... | ← Rủi ro bảo mật!
   │ 13      | Admin     | a@... | 092.. | 7f2a8c6e1b... | ← │
   └────────────────────────────────────────────────────────────┘
                      ▲▲▲
      Nếu user_role có SELECT ON users:
      • Khách A biết email của Khách B → spam
      • Hacker biết password hash → tấn công brute force
      • Employee biết lương của admin (nếu lưu ở đây)
      
   Giải pháp: Dùng VIEW an toàn
   ┌────────────────────────────────────────────────────────────┐
   │ CREATE VIEW vw_user_verification AS                         │
   │ SELECT user_id,                                             │
   │        full_name,                                           │
   │        MAX(CASE WHEN doc_type='CCCD' AND status='APPROVED'  │
   │            THEN 1 ELSE 0 END) AS cccd_verified              │
   │ FROM user_documents                                         │
   │ GROUP BY user_id, full_name;                                │
   └────────────────────────────────────────────────────────────┘
      VIEW này chỉ show: user_id, full_name, cccd_verified
      → Không lộ email, phone, password ✓


5. GRANT ON TABLE VS GRANT ON VIEW - SỰ KHÁC BIỆT VỀ BẢO MẬT
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   ┌─────────────────────────────────────────────────────────────┐
   │ GRANT SELECT ON USERS (BẢNG GỐC)                            │
   ├─────────────────────────────────────────────────────────────┤
   │ ✓ Có thể SELECT *                                           │
   │ ✗ Thấy toàn bộ cột: password_hash, secret_question, ...    │
   │ ✗ Thấy toàn bộ dòng: tất cả user                           │
   │ ✗ Không kiểm soát được nội dung                            │
   │ ✗ Rủi ro bảo mật cao                                        │
   └─────────────────────────────────────────────────────────────┘
   
   vs
   
   ┌─────────────────────────────────────────────────────────────┐
   │ GRANT SELECT ON VW_USER_VERIFICATION (VIEW)                 │
   ├─────────────────────────────────────────────────────────────┤
   │ ✓ Có thể SELECT * (nhưng từ VIEW, không phải TABLE)        │
   │ ✓ Chỉ thấy 3 cột: user_id, full_name, is_verified          │
   │ ✓ Chỉ thấy dòng có GROUP BY được                           │
   │ ✓ Được lọc/transform theo logic VIEW                       │
   │ ✓ Rủi ro bảo mật thấp                                       │
   │                                                             │
   │ VÍ DỤ: Nếu user_id=11, VIEW có thể chỉ show dòng của 11    │
   │ (thêm WHERE vào VIEW để filter)                            │
   └─────────────────────────────────────────────────────────────┘
   
   Bảng so sánh chi tiết:
   
   ┌──────────────────┬───────────────┬──────────────────────┐
   │ Tiêu chí         │ GRANT ON TABLE│ GRANT ON VIEW        │
   ├──────────────────┼───────────────┼──────────────────────┤
   │ Column filter    │ ✗ Thấy hết    │ ✓ Lọc theo WHERE    │
   │ Row filter       │ ✗ Thấy hết    │ ✓ Lọc theo WHERE    │
   │ Transformation   │ ✗ Không       │ ✓ Có (JOIN, CALC)   │
   │ Audit            │ ⚠ Khó trace   │ ✓ Dễ audit (VIEW)   │
   │ Performance      │ ✓ Nhanh (raw) │ ⚠ Chậm (JOIN)      │
   │ Security         │ ✗ Yếu         │ ✓ Mạnh              │
   └──────────────────┴───────────────┴──────────────────────┘


6. CHI TIẾT CẤP QUYỀN TRONG SCRIPT NÀY
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   user_role được cấp:
   ✓ SELECT ON: vehicles, reviews, vw_*, ...
   ✓ SELECT + INSERT ON: contracts, payments, conversations, messages
   ✗ Không: UPDATE, DELETE trên contracts/payments
   ✗ Không: SELECT trên users, user_documents, otp_codes
   
   admin_role được cấp:
   ✓ SELECT + INSERT + UPDATE + DELETE ON: tất cả bảng
   
   Lý do:
   - Khách hàng chỉ cần xem & tạo, không cần sửa/xóa
   - Admin cần quyền toàn bộ để xử lý sự cố, sửa dữ liệu sai
   - Bảng nhạy cảm (users, documents) không mở cho user_role

*/

-- ============================================================================
-- END OF SECURITY SCRIPT
-- ============================================================================

PROMPT;
PROMPT '✓ Script bảo mật và phân quyền đã hoàn thành!';
PROMPT;
PROMPT 'Bước tiếp theo:';
PROMPT '1. Tạo user cụ thể (nếu chưa có):';
PROMPT '   CREATE USER john IDENTIFIED BY password;';
PROMPT;
PROMPT '2. Gán role cho user:';
PROMPT '   GRANT user_role TO john;';
PROMPT;
PROMPT '3. Cấp quyền CREATE SESSION để user kết nối:';
PROMPT '   GRANT CREATE SESSION TO user_role;';
PROMPT;
PROMPT '═══════════════════════════════════════════════════════════════════════';
PROMPT;

COMMIT;

-- ============================================================================
