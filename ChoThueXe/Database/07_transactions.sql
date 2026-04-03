-- ============================================================================
-- FILE: 07_transactions.sql
-- PURPOSE: Thể hiện Transaction, SAVEPOINT và ROLLBACK trong Oracle 19c
-- DATABASE: Oracle 19c
-- AUTHOR: Database Team
-- CREATED: 2026-04-03
-- DESCRIPTION:
--   Script này minh họa cách sử dụng transaction với SAVEPOINT và ROLLBACK
--   tường minh trong Oracle Database. Ví dụ thực tế: tạo hợp đồng thuê xe
--   với chi tiết, nếu xảy ra lỗi thì rollback về checkpoint an toàn.
-- ============================================================================

-- Bật chế độ hiển thị output từ DBMS_OUTPUT
SET SERVEROUTPUT ON;
SET ECHO ON;

-- ============================================================================
-- DEMO 1: Transaction với SAVEPOINT và ROLLBACK - Kịch bản: Tạo hợp đồng thất bại
-- ============================================================================

DECLARE
  -- Khai báo biến lưu trữ ID hợp đồng
  v_contract_id NUMBER;
  
BEGIN
  -- Bước 1: In thông báo bắt đầu
  DBMS_OUTPUT.PUT_LINE('========== DEMO TRANSACTION SAVEPOINT + ROLLBACK ==========');
  DBMS_OUTPUT.PUT_LINE('Kịch bản: Tạo hợp đồng và chi tiết hợp đồng');
  DBMS_OUTPUT.PUT_LINE('Nếu lỗi xảy ra → rollback toàn bộ thay đổi');
  DBMS_OUTPUT.PUT_LINE('');

  -- Bước 2: Tạo SAVEPOINT trước khi bắt đầu các thao tác quan trọng
  -- SAVEPOINT là một điểm checkpoint, ta có thể ROLLBACK tới đây thay vì ROLLBACK toàn bộ
  SAVEPOINT before_create;
  DBMS_OUTPUT.PUT_LINE('[INFO] Tạo SAVEPOINT: before_create');

  -- Bước 3: Lấy ID hợp đồng mới từ sequence
  v_contract_id := seq_contracts.nextval;
  DBMS_OUTPUT.PUT_LINE('[INFO] Lấy contract_id từ seq_contracts: ' || v_contract_id);

  -- Bước 4: Thêm bản ghi hợp đồng vào bảng contracts
  -- Status = 'PENDING' vì hợp đồng vừa tạo chưa được duyệt
  INSERT INTO contracts (contract_id, customer_id, employee_id, status)
  VALUES (v_contract_id, 11, 3, 'PENDING');
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] INSERT contracts: contract_id=' || v_contract_id);

  -- Bước 5: Thêm chi tiết hợp đồng (số ngày thuê, số tiền, xe, ngày bắt đầu/kết thúc)
  -- Contract_detail_id cũng lấy từ seq_contracts (tạm thời không có seq riêng)
  INSERT INTO contract_details (
    contract_detail_id, 
    contract_id, 
    vehicle_id,
    start_date, 
    end_date, 
    price_per_day, 
    total_days, 
    amount
  )
  VALUES (
    seq_contracts.nextval,                           -- contract_detail_id tự sinh
    v_contract_id,                                   -- liên kết với hợp đồng vừa tạo
    1,                                               -- xe ID 1
    TRUNC(SYSDATE),                                  -- ngày bắt đầu hôm nay
    TRUNC(SYSDATE) + 3,                              -- ngày kết thúc 3 ngày sau
    500000,                                          -- 500k VND/ngày
    TRUNC(SYSDATE + 3) - TRUNC(SYSDATE),            -- tính tổng ngày
    (TRUNC(SYSDATE + 3) - TRUNC(SYSDATE)) * 500000  -- tổng tiền
  );
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] INSERT contract_details: amount=1500000 VND');

  -- Bước 6: MÌNH SẼ TẠO LỖI ĐỂ KIỂM TRA ROLLBACK
  -- Trong thực tế, đây có thể là lỗi từ database constraint, business logic, etc.
  -- raise_application_error(-20099, ...) tạo ra lỗi người dùng
  -- -20000 tới -20999 là dải dành cho application errors
  DBMS_OUTPUT.PUT_LINE('[ERROR] Mô phỏng lỗi hệ thống xảy ra!');
  raise_application_error(-20099, 'Test rollback - mo phong loi he thong');

  -- Bước 7: COMMIT - SẼ KHÔNG BAO GIỜ CHẠY TỚI ĐÂY vì lỗi xảy ra ở bước 6
  COMMIT;
  DBMS_OUTPUT.PUT_LINE('[INFO] COMMIT thành công - Dữ liệu đã lưu vào DB');

EXCEPTION
  -- Xử lý ngoại lệ/lỗi
  WHEN OTHERS THEN
    -- ROLLBACK TO <savepoint_name> cuộn ngược tới checkpoint được đặt tên
    -- Điều này khác với ROLLBACK toàn bộ - chỉ cuộn ngược các thay đổi sau SAVEPOINT
    ROLLBACK TO before_create;
    
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('[ROLLBACK] Đã cuộn ngược tới SAVEPOINT: before_create');
    DBMS_OUTPUT.PUT_LINE('[ROLLBACK] Toàn bộ INSERT vừa thực hiện đều KHÔNG có hiệu lực');
    DBMS_OUTPUT.PUT_LINE('[ROLLBACK] Lỗi chi tiết: ' || SQLERRM);
    DBMS_OUTPUT.PUT_LINE('========== KẾT THÚC DEMO ==========');

END;
/

-- ============================================================================
-- DEMO 2: Transaction thành công (không có lỗi)
-- ============================================================================

DECLARE
  v_contract_id NUMBER;
  
BEGIN
  DBMS_OUTPUT.PUT_LINE('');
  DBMS_OUTPUT.PUT_LINE('========== DEMO TRANSACTION THÀNH CÔNG ==========');
  DBMS_OUTPUT.PUT_LINE('Kịch bản: Tạo hợp đồng và chi tiết mà KHÔNG có lỗi');
  DBMS_OUTPUT.PUT_LINE('');

  -- Tạo SAVEPOINT an toàn
  SAVEPOINT before_create_success;
  DBMS_OUTPUT.PUT_LINE('[INFO] Tạo SAVEPOINT: before_create_success');

  -- Lấy ID hợp đồng mới
  v_contract_id := seq_contracts.nextval;
  DBMS_OUTPUT.PUT_LINE('[INFO] Lấy contract_id từ seq_contracts: ' || v_contract_id);

  -- INSERT hợp đồng
  INSERT INTO contracts (contract_id, customer_id, employee_id, status)
  VALUES (v_contract_id, 12, 3, 'PENDING');
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] INSERT contracts: contract_id=' || v_contract_id);

  -- INSERT chi tiết hợp đồng
  INSERT INTO contract_details (
    contract_detail_id, 
    contract_id, 
    vehicle_id,
    start_date, 
    end_date, 
    price_per_day, 
    total_days, 
    amount
  )
  VALUES (
    seq_contracts.nextval,
    v_contract_id,
    2,
    TRUNC(SYSDATE),
    TRUNC(SYSDATE) + 5,
    750000,
    TRUNC(SYSDATE + 5) - TRUNC(SYSDATE),
    (TRUNC(SYSDATE + 5) - TRUNC(SYSDATE)) * 750000
  );
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] INSERT contract_details: amount=3750000 VND');

  -- LẦN NÀY KHÔNG CÓ RAISE ERROR → COMMIT THÀNH CÔNG
  COMMIT;
  DBMS_OUTPUT.PUT_LINE('[COMMIT] ✓ Toàn bộ transaction đã được lưu vào database');
  DBMS_OUTPUT.PUT_LINE('========== KẾT THÚC DEMO ==========');

EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK TO before_create_success;
    DBMS_OUTPUT.PUT_LINE('[ERROR] Lỗi: ' || SQLERRM);

END;
/

-- ============================================================================
-- GIẢI THÍCH KHÁI NIỆM TRANSACTION TRONG ORACLE 19c
-- ============================================================================

/*
╔════════════════════════════════════════════════════════════════════════════╗
║                 TRANSACTION - SAVEPOINT - ROLLBACK TRONG ORACLE             ║
╚════════════════════════════════════════════════════════════════════════════╝

1. ISOLATION LEVEL MẶC ĐỊNH CỦA ORACLE
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Oracle 19c mặc định sử dụng: READ COMMITTED
   
   - READ COMMITTED:
     • Session A thấy dữ liệu mà Session B đã COMMIT
     • Session A KHÔNG thấy uncommitted data từ Session B (Dirty Read tránh được)
     • Nhưng Session A có thể thấy dữ liệu thay đổi giữa các lần đọc (Non-repeatable Read)
     • Phù hợp với: Hầu hết ứng dụng thương mại, hệ thống cho thuê xe
   
   - Mức cao hơn: SERIALIZABLE (tuyến tính, chậm hơn)
     SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
   
   - Mức thấp hơn: DIRTY READ (không tồn tại trong Oracle, chỉ có trong DB khác)


2. SAVEPOINT vs ROLLBACK TOÀN BỘ
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   SAVEPOINT before_create;
   INSERT INTO contracts ... (Thao tác 1)
   INSERT INTO contract_details ... (Thao tác 2)
   raise_application_error(...); (Xảy ra lỗi)
   ROLLBACK TO before_create; ← Chỉ cuộn ngược Thao tác 1 & 2, trở về trạng thái SAVEPOINT
   
   So sánh:
   ┌─────────────────────────────────────────────────────────────────┐
   │ ROLLBACK TO savepoint_name                                      │
   ├─────────────────────────────────────────────────────────────────┤
   │ + Cuộn ngược chỉ tới SAVEPOINT (các thay đổi trước SAVEPOINT    │
   │   vẫn còn)                                                      │
   │ + Chi phí ít hơn (không phải cuộn ngược toàn bộ)               │
   │ + Có thể tạo nhiều SAVEPOINT lồng nhau                          │
   │ + Transaction vẫn ACTIVE (chưa kết thúc)                       │
   │                                                                 │
   │ vs ROLLBACK (toàn bộ)                                           │
   ├─────────────────────────────────────────────────────────────────┤
   │ - Cuộn ngược TẤT CẢ thay đổi kể từ BEGIN/START TRANSACTION     │
   │ - Transaction kết thúc hoàn toàn                               │
   │ - Chi phí cao hơn nếu có nhiều thay đổi                        │
   └─────────────────────────────────────────────────────────────────┘


3. THỰC TẾ: KHI NÀO DÙNG SAVEPOINT?
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   Ví dụ 1: Thuê nhiều xe 1 lần (Ứng dụng: Hợp đồng gộp nhiều xe)
   ┌─────────────────────────────────────────────────────────────────┐
   │ BEGIN                                                           │
   │   SAVEPOINT after_vehicle_1;                                    │
   │   INSERT contracts (xe 1) + INSERT contract_details (xe 1)      │
   │   -- Nếu lỗi xe 2 → ROLLBACK TO after_vehicle_1, xe 1 vẫn OK   │
   │                                                                 │
   │   SAVEPOINT after_vehicle_2;                                    │
   │   INSERT contracts (xe 2) + INSERT contract_details (xe 2)      │
   │   -- Nếu lỗi xe 3 → ROLLBACK TO after_vehicle_2, xe 1,2 vẫn OK │
   │                                                                 │
   │   INSERT contracts (xe 3) + INSERT contract_details (xe 3)      │
   │   COMMIT;                                                       │
   │ END;                                                            │
   └─────────────────────────────────────────────────────────────────┘
   
   Ví dụ 2: Duyệt tài liệu khách hàng
   ┌─────────────────────────────────────────────────────────────────┐
   │ BEGIN                                                           │
   │   SAVEPOINT before_cccd_check;                                  │
   │   UPDATE user_documents SET status='APPROVED' WHERE doc_type... │
   │   -- Nếu check thất bại → ROLLBACK TO before_cccd_check        │
   │                                                                 │
   │   SAVEPOINT before_license_check;                               │
   │   UPDATE user_documents SET status='APPROVED' WHERE doc_type... │
   │   -- Nếu check thất bại → ROLLBACK TO before_license_check,    │
   │   -- CCCD vẫn được duyệt                                        │
   │                                                                 │
   │   UPDATE users SET is_verified=1 WHERE ...                      │
   │   COMMIT;                                                       │
   │ END;                                                            │
   └─────────────────────────────────────────────────────────────────┘
   
   Lợi ích:
   ✓ Phục hồi từng phần → Chi phí ít hơn
   ✓ Cho phép tiếp tục xử lý phần còn lại
   ✓ Ghi log chi tiết: "Từng bước nào thành công, bước nào thất bại"
   ✓ Tăng tính kiểm soát trong quy trình phức tạp


4. MẪUCÙNG DÙNG TRONG ỨNG DỤNG C# (DATA LAYER)
   ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   
   using (var connection = new OracleConnection(connStr))
   {
       await connection.OpenAsync();
       using (var transaction = connection.BeginTransaction(
           IsolationLevel.ReadCommitted))  // ← Thiết lập isolation level
       {
           try
           {
               // INSERT contracts
               await contractCommand.ExecuteNonQueryAsync();
               
               // INSERT contract_details
               await detailsCommand.ExecuteNonQueryAsync();
               
               await transaction.CommitAsync();
           }
           catch (Exception ex)
           {
               await transaction.RollbackAsync();
               // Log lỗi và thông báo cho user
           }
       }
   }

*/

-- ============================================================================
-- KIỂM TRA KẾT QUẢ
-- ============================================================================

PROMPT;
PROMPT '=== KIỂM TRA DỮ LIỆU SAU DEMO ===';
PROMPT;

-- Xem các hợp đồng mới tạo (từ demo thành công)
SELECT 
  contract_id,
  customer_id,
  employee_id,
  status,
  contract_date
FROM contracts
WHERE contract_date >= TRUNC(SYSDATE)
ORDER BY contract_id DESC;

PROMPT;
PROMPT '=== CHI TIẾT HỢP ĐỒNG ===';
PROMPT;

SELECT 
  cd.contract_detail_id,
  cd.contract_id,
  cd.vehicle_id,
  cd.start_date,
  cd.end_date,
  cd.amount
FROM contract_details cd
WHERE cd.contract_id IN (
  SELECT c.contract_id 
  FROM contracts c 
  WHERE c.contract_date >= TRUNC(SYSDATE)
)
ORDER BY cd.contract_id DESC;

-- ============================================================================
