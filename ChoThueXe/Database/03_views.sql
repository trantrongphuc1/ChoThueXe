-- ============================================================================
-- FILE: 03_views.sql
-- PURPOSE: Tạo các VIEW cho hệ thống cho thuê xe (ChoThueXe)
-- DATABASE: Oracle 19c
-- AUTHOR: Database Team
-- CREATED: 2026-04-03
-- DESCRIPTION: 
--   Script này định nghĩa 4 view chính phục vụ các chức năng:
--   1. vw_vehicle_detail - Hiển thị thông tin xe chi tiết
--   2. vw_contract_full - Xem toàn bộ thông tin hợp đồng
--   3. vw_user_verification - Kiểm tra trạng thái xác minh giấy tờ
--   4. vw_revenue - Thống kê doanh thu theo xe
-- ============================================================================

-- Mục đích: Hiển thị thông tin xe kèm tên hãng và loại xe cho client
-- Sử dụng: RentalRepository.GetVehiclesAsync(), tìm kiếm xe, lọc xe theo amenities
CREATE OR REPLACE VIEW vw_vehicle_detail AS
  SELECT 
    v.vehicle_id,
    v.vehicle_name,
    b.brand_name,
    t.type_name,
    v.price_per_day,
    v.status,
    v.seats,
    v.transmission,
    v.fuel_type,
    v.owner_id
  FROM vehicles v
  INNER JOIN brands b ON v.brand_id = b.brand_id
  INNER JOIN vehicle_types t ON v.type_id = t.type_id;

-- Mục đích: Xem toàn bộ thông tin hợp đồng kèm khách hàng và xe
-- Sử dụng: Admin dashboard, xem lịch sử hợp đồng, báo cáo quản lý
CREATE OR REPLACE VIEW vw_contract_full AS
  SELECT 
    c.contract_id,
    c.customer_id,
    u.full_name AS customer_name,
    u.email AS customer_email,
    u.phone AS customer_phone,
    c.employee_id,
    cd.vehicle_id,
    v.vehicle_name,
    v.brand_id,
    b.brand_name,
    cd.start_date,
    cd.end_date,
    cd.total_days,
    cd.amount AS rental_amount,
    c.total_amount,
    c.status AS contract_status,
    c.contract_date,
    TRUNC(SYSDATE) - TRUNC(cd.start_date) AS days_since_start
  FROM contracts c
  INNER JOIN users u ON c.customer_id = u.user_id
  INNER JOIN contract_details cd ON c.contract_id = cd.contract_id
  INNER JOIN vehicles v ON cd.vehicle_id = v.vehicle_id
  LEFT JOIN brands b ON v.brand_id = b.brand_id;

-- Mục đích: Kiểm tra trạng thái xác minh giấy tờ của từng user
-- Sử dụng: Kiểm tra user đã được duyệt hồ sơ hay chưa trước khi cho thuê
CREATE OR REPLACE VIEW vw_user_verification AS
  SELECT 
    ud.user_id,
    u.full_name,
    u.email,
    MAX(CASE 
      WHEN UPPER(ud.doc_type) IN ('CCCD', 'ID_CARD') AND UPPER(ud.status) = 'APPROVED' THEN 1
      ELSE 0 
    END) AS cccd_verified,
    MAX(CASE 
      WHEN UPPER(ud.doc_type) IN ('DRIVER_LICENSE', 'DRIVER_LICENSES') AND UPPER(ud.status) = 'APPROVED' THEN 1
      ELSE 0 
    END) AS license_verified,
    CASE 
      WHEN COUNT(CASE WHEN UPPER(ud.status) = 'APPROVED' THEN 1 END) > 0 THEN 1
      ELSE 0 
    END AS is_verified,
    MAX(ud.status) AS latest_verification_status,
    MAX(ud.created_at) AS latest_verification_date
  FROM user_documents ud
  INNER JOIN users u ON ud.user_id = u.user_id
  GROUP BY ud.user_id, u.full_name, u.email;

-- Mục đích: Thống kê doanh thu theo từng xe, chỉ tính hợp đồng đã hoàn thành
-- Sử dụng: Báo cáo doanh thu, phân tích hiệu suất xe, xếp hạng xe có doanh thu cao nhất
CREATE OR REPLACE VIEW vw_revenue AS
  SELECT 
    v.vehicle_id,
    v.vehicle_name,
    b.brand_name,
    t.type_name,
    COUNT(DISTINCT c.contract_id) AS total_contracts_completed,
    SUM(cd.amount) AS total_revenue,
    ROUND(AVG(cd.amount), 2) AS avg_rental_value,
    SUM(cd.total_days) AS total_rental_days,
    ROUND(SUM(cd.amount) / NULLIF(SUM(cd.total_days), 0), 2) AS revenue_per_day
  FROM contracts c
  INNER JOIN contract_details cd ON c.contract_id = cd.contract_id
  INNER JOIN vehicles v ON cd.vehicle_id = v.vehicle_id
  LEFT JOIN brands b ON v.brand_id = b.brand_id
  LEFT JOIN vehicle_types t ON v.type_id = t.type_id
  WHERE UPPER(c.status) IN ('COMPLETED', 'DONE', 'FINISHED', 'PAID')
  GROUP BY v.vehicle_id, v.vehicle_name, b.brand_name, t.type_name;

-- ============================================================================
-- VERIFICATION QUERIES - Test các view sau khi tạo
-- ============================================================================

COMMIT;

-- Test vw_vehicle_detail: Kiểm tra thông tin chi tiết của các xe
SELECT '=== VIEW: vw_vehicle_detail ===' AS test_view FROM dual;
SELECT * FROM vw_vehicle_detail 
WHERE ROWNUM <= 5;

-- Test vw_contract_full: Kiểm tra thông tin hợp đồng đầy đủ
SELECT '=== VIEW: vw_contract_full ===' AS test_view FROM dual;
SELECT * FROM vw_contract_full 
WHERE ROWNUM <= 5;

-- Test vw_user_verification: Kiểm tra trạng thái xác minh user
SELECT '=== VIEW: vw_user_verification ===' AS test_view FROM dual;
SELECT * FROM vw_user_verification 
WHERE ROWNUM <= 5;

-- Test vw_revenue: Kiểm tra doanh thu theo xe
SELECT '=== VIEW: vw_revenue ===' AS test_view FROM dual;
SELECT * FROM vw_revenue 
ORDER BY total_revenue DESC 
NULLS LAST;

-- Query kiểm tra tất cả view đã tạo
SELECT view_name, text_length 
FROM user_views 
WHERE view_name IN ('VW_VEHICLE_DETAIL', 'VW_CONTRACT_FULL', 'VW_USER_VERIFICATION', 'VW_REVENUE')
ORDER BY view_name;

-- ============================================================================
