-- ============================================================================
-- FILE: 09_sample_data.sql
-- PURPOSE: Seed dữ liệu mẫu cho hệ thống cho thuê xe (ChoThueXe)
-- DATABASE: Oracle 19c
-- AUTHOR: Database Team / DBA
-- CREATED: 2026-04-03
-- DESCRIPTION:
--   Script này insert >=100 record dữ liệu mẫu để test hệ thống.
--   Dữ liệu được insert theo thứ tự FK (Foreign Key) để tránh lỗi constraint.
--   
--   BƯỚC 1-4: Dữ liệu nền tảng (roles, users, documents, brands, types)
--   BƯỚC 5-8: Dữ liệu giao dịch (vehicles, contracts, contract_details, payments)
--   BƯỚC 9-10: Dữ liệu bổ sung (conversations, messages, reviews)
--   
--   Tổng số record dự kiến: ~130 records
-- ============================================================================

SET ECHO ON;
SET SERVEROUTPUT ON;

PROMPT;
PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ BẮT ĐẦU SEED DỮ LIỆU MẪU - HỆ THỐNG CHO THUÊ XE                 ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

-- ============================================================================
-- BƯỚC 1: INSERT ROLES (3 records)
-- ============================================================================
-- Mục đích: Tạo các vai trò cơ bản cho hệ thống
-- Records dự kiến: 3

PROMPT '════ BƯỚC 1: INSERT ROLES (3 records) ════';

BEGIN
  INSERT INTO roles (role_id, role_name) VALUES (1, 'ADMIN');
  INSERT INTO roles (role_id, role_name) VALUES (2, 'CUSTOMER');
  INSERT INTO roles (role_id, role_name) VALUES (3, 'EMPLOYEE');
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 3 roles');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 2: INSERT USERS (20 records)
-- ============================================================================
-- Mục đích: Tạo 20 user với các role khác nhau
-- Phân bổ:
--   - user_id 1-2: ADMIN (role_id=1)
--   - user_id 3-10: EMPLOYEE (role_id=3)
--   - user_id 11-20: CUSTOMER (role_id=2)
-- Records dự kiến: 20

PROMPT '════ BƯỚC 2: INSERT USERS (20 records) ════';

DECLARE
  v_role_id NUMBER;
  v_user_id NUMBER;
  v_email VARCHAR2(100);
  v_phone VARCHAR2(20);
  v_password VARCHAR2(255);
  v_full_name VARCHAR2(100);
  
BEGIN
  FOR i IN 1..20 LOOP
    v_user_id := i;
    v_email := 'user' || i || '@rentalcar.com';
    v_phone := '090000000' || i;
    v_password := 'hashed_123456';
    v_full_name := 'User ' || i;
    
    -- Phân bổ role dựa trên user_id
    IF i <= 2 THEN
      v_role_id := 1;  -- ADMIN
    ELSIF i <= 10 THEN
      v_role_id := 3;  -- EMPLOYEE
    ELSE
      v_role_id := 2;  -- CUSTOMER
    END IF;
    
    INSERT INTO users (user_id, role_id, full_name, email, password, phone)
    VALUES (v_user_id, v_role_id, v_full_name, v_email, v_password, v_phone);
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 20 users');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 3: INSERT USER_DOCUMENTS (10 records)
-- ============================================================================
-- Mục đích: Tạo tài liệu xác minh cho khách hàng
-- Chi tiết:
--   - user_id 11-20 (CUSTOMER)
--   - doc_type: 'CCCD'
--   - status: 'APPROVED' (để họ được phép tạo hợp đồng)
-- Records dự kiến: 10

PROMPT '════ BƯỚC 3: INSERT USER_DOCUMENTS (10 records) ════';

DECLARE
  v_doc_id NUMBER;
  v_user_id NUMBER;
  
BEGIN
  FOR i IN 11..20 LOOP
    v_doc_id := i;
    v_user_id := i;
    
    INSERT INTO user_documents (document_id, user_id, doc_type, status, created_at)
    VALUES (v_doc_id, v_user_id, 'CCCD', 'APPROVED', SYSDATE);
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 10 user_documents');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 4: INSERT BRANDS + VEHICLE_TYPES (6 records)
-- ============================================================================
-- Mục đích: Tạo danh sách hãng xe và loại xe
-- Records dự kiến: 3 brands + 3 vehicle_types = 6

PROMPT '════ BƯỚC 4: INSERT BRANDS + VEHICLE_TYPES (6 records) ════';

BEGIN
  -- Thêm brands
  INSERT INTO brands (brand_id, brand_name) VALUES (1, 'Toyota');
  INSERT INTO brands (brand_id, brand_name) VALUES (2, 'Honda');
  INSERT INTO brands (brand_id, brand_name) VALUES (3, 'Ford');

  -- Thêm vehicle_types
  INSERT INTO vehicle_types (type_id, type_name) VALUES (1, 'Sedan');
  INSERT INTO vehicle_types (type_id, type_name) VALUES (2, 'SUV');
  INSERT INTO vehicle_types (type_id, type_name) VALUES (3, 'Hatchback');

  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 3 brands + 3 vehicle_types');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 5: INSERT VEHICLES (30 records)
-- ============================================================================
-- Mục đích: Tạo 30 xe trong hệ thống
-- Chi tiết:
--   - owner_id: 1 (admin là chủ sở hữu)
--   - brand_id, type_id: luân phiên 1-3
--   - price_per_day: tăng dần 300000 + i*10000
--   - status: 'AVAILABLE'
-- Records dự kiến: 30

PROMPT '════ BƯỚC 5: INSERT VEHICLES (30 records) ════';

DECLARE
  v_vehicle_id NUMBER;
  v_brand_id NUMBER;
  v_type_id NUMBER;
  v_price_per_day NUMBER;
  v_vehicle_name VARCHAR2(100);
  
BEGIN
  FOR i IN 1..30 LOOP
    v_vehicle_id := i;
    v_brand_id := MOD(i - 1, 3) + 1;  -- Luân phiên 1, 2, 3
    v_type_id := MOD(i - 1, 3) + 1;   -- Luân phiên 1, 2, 3
    v_price_per_day := 300000 + (i * 10000);
    v_vehicle_name := 'Car ' || i;
    
    INSERT INTO vehicles (
      vehicle_id, owner_id, brand_id, type_id, vehicle_name,
      license_plate, seats, transmission, fuel_type, price_per_day, status
    )
    VALUES (
      v_vehicle_id, 1, v_brand_id, v_type_id, v_vehicle_name,
      'LP-' || LPAD(i, 5, '0'), 4, 'Auto', 'Petrol', v_price_per_day, 'AVAILABLE'
    );
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 30 vehicles');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 6: INSERT CONTRACTS (40 records)
-- ============================================================================
-- Mục đích: Tạo 40 hợp đồng
-- Chi tiết:
--   - contract_id: từ seq_contracts.nextval
--   - customer_id: luân phiên 11-20
--   - employee_id: luân phiên 3-10
--   - contract_date: SYSDATE - i
--   - status: 'COMPLETED' nếu i chẵn, 'ACTIVE' nếu i lẻ
-- Records dự kiến: 40

PROMPT '════ BƯỚC 6: INSERT CONTRACTS (40 records) ════';

DECLARE
  v_contract_id NUMBER;
  v_customer_id NUMBER;
  v_employee_id NUMBER;
  v_status VARCHAR2(20);
  v_contract_date DATE;
  
BEGIN
  FOR i IN 1..40 LOOP
    v_contract_id := seq_contracts.nextval;
    v_customer_id := 11 + MOD(i - 1, 10);  -- Luân phiên 11-20
    v_employee_id := 3 + MOD(i - 1, 8);    -- Luân phiên 3-10
    v_contract_date := SYSDATE - i;
    
    -- Phân bổ status
    IF MOD(i, 2) = 0 THEN
      v_status := 'COMPLETED';
    ELSE
      v_status := 'ACTIVE';
    END IF;
    
    INSERT INTO contracts (
      contract_id, customer_id, employee_id, contract_date, status, total_amount
    )
    VALUES (
      v_contract_id, v_customer_id, v_employee_id, v_contract_date, v_status, 900000
    );
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 40 contracts');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 7: INSERT CONTRACT_DETAILS (40 records)
-- ============================================================================
-- Mục đích: Tạo chi tiết cho mỗi hợp đồng
-- Chi tiết:
--   - 1 chi tiết mỗi hợp đồng
--   - vehicle_id: luân phiên 1-30
--   - start_date: SYSDATE-5
--   - end_date: SYSDATE-2
--   - price_per_day: 300000
--   - total_days: 3
--   - amount: 900000
-- Records dự kiến: 40

PROMPT '════ BƯỚC 7: INSERT CONTRACT_DETAILS (40 records) ════';

DECLARE
  v_detail_id NUMBER;
  v_contract_id NUMBER;
  v_vehicle_id NUMBER;
  v_cnt NUMBER := 0;
  
BEGIN
  FOR r IN (SELECT contract_id FROM contracts ORDER BY contract_id) LOOP
    v_detail_id := seq_contracts.nextval;
    v_contract_id := r.contract_id;
    v_vehicle_id := MOD(r.contract_id - 1, 30) + 1;  -- Luân phiên 1-30
    v_cnt := v_cnt + 1;
    
    INSERT INTO contract_details (
      contract_detail_id, contract_id, vehicle_id,
      start_date, end_date, price_per_day, total_days, amount
    )
    VALUES (
      v_detail_id, v_contract_id, v_vehicle_id,
      TRUNC(SYSDATE) - 5, TRUNC(SYSDATE) - 2, 300000, 3, 900000
    );
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted ' || v_cnt || ' contract_details');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 8: INSERT PAYMENTS (40 records)
-- ============================================================================
-- Mục đích: Ghi nhận thanh toán cho mỗi hợp đồng
-- Chi tiết:
--   - payment_id: từ seq_payments.nextval
--   - amount: 900000
--   - payment_method: 'CASH'
--   - payment_date: SYSDATE-1
--   - status: 'PAID'
-- Records dự kiến: 40

PROMPT '════ BƯỚC 8: INSERT PAYMENTS (40 records) ════';

DECLARE
  v_payment_id NUMBER;
  v_contract_id NUMBER;
  v_cnt NUMBER := 0;
  
BEGIN
  FOR r IN (SELECT contract_id FROM contracts ORDER BY contract_id) LOOP
    v_payment_id := seq_payments.nextval;
    v_contract_id := r.contract_id;
    v_cnt := v_cnt + 1;
    
    INSERT INTO payments (
      payment_id, contract_id, amount, payment_method, payment_date, status
    )
    VALUES (
      v_payment_id, v_contract_id, 900000, 'CASH', TRUNC(SYSDATE) - 1, 'PAID'
    );
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted ' || v_cnt || ' payments');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 9: INSERT CONVERSATIONS + MESSAGES (10 records mỗi bảng)
-- ============================================================================
-- Mục đích: Tạo hội thoại và tin nhắn giữa khách hàng và admin
-- Chi tiết:
--   - conversation_id: 1-10
--   - user_id: 10+i (11-20)
--   - admin_id: 1
--   - vehicle_id: i (1-10)
--   - status: 'OPEN'
--   - message_id: 1-10
--   - sender_id: 10+i
--   - content: 'Xin hoi ve xe so ' || i
-- Records dự kiến: 10 conversations + 10 messages = 20

PROMPT '════ BƯỚC 9: INSERT CONVERSATIONS + MESSAGES (20 records) ════';

DECLARE
  v_conversation_id NUMBER;
  v_message_id NUMBER;
  v_user_id NUMBER;
  v_sender_id NUMBER;
  v_content VARCHAR2(500);
  
BEGIN
  FOR i IN 1..10 LOOP
    v_conversation_id := i;
    v_user_id := 10 + i;        -- 11-20
    v_sender_id := 10 + i;      -- 11-20
    v_content := 'Xin hoi ve xe so ' || i;
    
    -- Insert conversation
    INSERT INTO conversations (
      conversation_id, user_id, admin_id, vehicle_id, status, created_at
    )
    VALUES (
      v_conversation_id, v_user_id, 1, i, 'OPEN', SYSDATE
    );
    
    -- Insert message
    INSERT INTO messages (
      message_id, conversation_id, sender_id, content, sent_at
    )
    VALUES (
      seq_messages.nextval, v_conversation_id, v_sender_id, v_content, SYSDATE
    );
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted 10 conversations + 10 messages');
END;
/

PROMPT;

-- ============================================================================
-- BƯỚC 10: INSERT REVIEWS (từ contract hoàn thành)
-- ============================================================================
-- Mục đích: Tạo review từ các hợp đồng đã hoàn thành
-- Chi tiết:
--   - review_id: ROWNUM
--   - vehicle_id: từ contract_details
--   - user_id: từ contracts (customer_id)
--   - rating: 1-5 (luân phiên)
--   - review_text: "Xe tot, dich vu chuyen nghiep"
-- Records dự kiến: ~20 (tùy số lượng hợp đồng COMPLETED)

PROMPT '════ BƯỚC 10: INSERT REVIEWS (~20 records) ════';

DECLARE
  v_review_id NUMBER;
  v_cnt NUMBER := 0;
  
BEGIN
  FOR r IN (
    SELECT c.contract_id, cd.vehicle_id, c.customer_id
    FROM contracts c
    JOIN contract_details cd ON c.contract_id = cd.contract_id
    WHERE UPPER(c.status) = 'COMPLETED'
  ) LOOP
    v_review_id := NVL((SELECT MAX(review_id) FROM reviews), 0) + 1;
    v_cnt := v_cnt + 1;
    
    INSERT INTO reviews (
      review_id, vehicle_id, user_id, rating, review_text, created_at
    )
    VALUES (
      v_review_id, r.vehicle_id, r.customer_id,
      MOD(v_cnt, 5) + 1,  -- Rating 1-5
      'Xe tot, dich vu chuyen nghiep',
      SYSDATE
    );
  END LOOP;
  
  DBMS_OUTPUT.PUT_LINE('[SUCCESS] Inserted ' || v_cnt || ' reviews');
END;
/

PROMPT;

-- ============================================================================
-- COMMIT - LƯU VÀO DATABASE
-- ============================================================================

BEGIN
  COMMIT;
  DBMS_OUTPUT.PUT_LINE('[COMMIT] Toàn bộ dữ liệu đã được lưu');
END;
/

PROMPT;
PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ KIỂM TRA SỐ LƯỢNG RECORD TỪng BẢNG                                ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

-- ============================================================================
-- VERIFICATION: Kiểm tra số lượng từng bảng
-- ============================================================================

SELECT bang, so_luong
FROM (
  SELECT 'roles' AS bang, COUNT(*) AS so_luong FROM roles
  UNION ALL
  SELECT 'users', COUNT(*) FROM users
  UNION ALL
  SELECT 'user_documents', COUNT(*) FROM user_documents
  UNION ALL
  SELECT 'brands', COUNT(*) FROM brands
  UNION ALL
  SELECT 'vehicle_types', COUNT(*) FROM vehicle_types
  UNION ALL
  SELECT 'vehicles', COUNT(*) FROM vehicles
  UNION ALL
  SELECT 'contracts', COUNT(*) FROM contracts
  UNION ALL
  SELECT 'contract_details', COUNT(*) FROM contract_details
  UNION ALL
  SELECT 'payments', COUNT(*) FROM payments
  UNION ALL
  SELECT 'conversations', COUNT(*) FROM conversations
  UNION ALL
  SELECT 'messages', COUNT(*) FROM messages
  UNION ALL
  SELECT 'reviews', COUNT(*) FROM reviews
)
ORDER BY bang;

PROMPT;
PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ TÍNH TOÁN TỔNG SỐ RECORD                                          ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

SELECT 
  (SELECT COUNT(*) FROM roles) +
  (SELECT COUNT(*) FROM users) +
  (SELECT COUNT(*) FROM user_documents) +
  (SELECT COUNT(*) FROM brands) +
  (SELECT COUNT(*) FROM vehicle_types) +
  (SELECT COUNT(*) FROM vehicles) +
  (SELECT COUNT(*) FROM contracts) +
  (SELECT COUNT(*) FROM contract_details) +
  (SELECT COUNT(*) FROM payments) +
  (SELECT COUNT(*) FROM conversations) +
  (SELECT COUNT(*) FROM messages) +
  (SELECT COUNT(*) FROM reviews) AS total_records
FROM dual;

PROMPT;
PROMPT '✓ Seed dữ liệu mẫu hoàn thành!';
PROMPT;

-- ============================================================================
-- KIỂM TRA CONSTRAINT CÓ NHẤT QUÁN KHÔNG
-- ============================================================================

PROMPT;
PROMPT '╔════════════════════════════════════════════════════════════════════╗';
PROMPT '║ KIỂM TRA TÍNH NHẤT QUÁN (FK, Constraints)                         ║';
PROMPT '╚════════════════════════════════════════════════════════════════════╝';
PROMPT;

-- Kiểm tra: Tất cả customer_id trong contracts phải tồn tại trong users
PROMPT 'Kiểm tra FK: contracts.customer_id → users.user_id';
SELECT 
  COUNT(*) AS orphaned_contracts
FROM contracts c
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.user_id = c.customer_id);

-- Kiểm tra: contract_details.start_date <= end_date
PROMPT 'Kiểm tra: contract_details.start_date <= end_date';
SELECT 
  COUNT(*) AS invalid_dates
FROM contract_details
WHERE start_date > end_date;

-- Kiểm tra: user_documents status là APPROVED cho những user có contract
PROMPT 'Kiểm tra: customers có document APPROVED';
SELECT 
  COUNT(DISTINCT c.customer_id) AS customers_with_approved_docs
FROM contracts c
WHERE EXISTS (
  SELECT 1 FROM user_documents ud
  WHERE ud.user_id = c.customer_id
    AND UPPER(ud.status) = 'APPROVED'
);

PROMPT;
PROMPT '✓ Kiểm tra tính nhất quán hoàn thành!';
PROMPT;
PROMPT '════════════════════════════════════════════════════════════════════════';
PROMPT;

-- ============================================================================
