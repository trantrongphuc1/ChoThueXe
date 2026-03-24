-- Script: init_feature_schema.sql
-- Creates tables and sequences used by the application features.

-- favorite_vehicles table
BEGIN
    EXECUTE IMMEDIATE 'CREATE TABLE favorite_vehicles (
        favorite_id NUMBER PRIMARY KEY,
        user_id NUMBER NOT NULL,
        vehicle_id NUMBER NOT NULL,
        created_at DATE DEFAULT SYSDATE
    )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'CREATE SEQUENCE favorite_vehicles_seq START WITH 1 INCREMENT BY 1 NOCACHE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

-- otp_codes table
BEGIN
    EXECUTE IMMEDIATE 'CREATE TABLE otp_codes (
        otp_id NUMBER PRIMARY KEY,
        user_id NUMBER NULL,
        email VARCHAR2(255) NOT NULL,
        otp_code VARCHAR2(20) NOT NULL,
        expires_at DATE NOT NULL,
        is_used NUMBER(1) DEFAULT 0 NOT NULL,
        created_at DATE DEFAULT SYSDATE
    )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'CREATE SEQUENCE otp_codes_seq START WITH 1 INCREMENT BY 1 NOCACHE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

-- activity_logs table
BEGIN
    EXECUTE IMMEDIATE 'CREATE TABLE activity_logs (
        activity_id NUMBER PRIMARY KEY,
        user_id NUMBER NULL,
        action VARCHAR2(100) NOT NULL,
        details VARCHAR2(2000) NULL,
        created_at DATE DEFAULT SYSDATE
    )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'CREATE SEQUENCE activity_logs_seq START WITH 1 INCREMENT BY 1 NOCACHE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

-- drive_licenses table
BEGIN
    EXECUTE IMMEDIATE 'CREATE TABLE drive_licenses (
        drive_license_id NUMBER PRIMARY KEY,
        user_id NUMBER NOT NULL,
        license_number VARCHAR2(100) NOT NULL,
        issued_by VARCHAR2(255),
        issued_at DATE,
        expire_at DATE,
        created_at DATE DEFAULT SYSDATE
    )';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'CREATE SEQUENCE drive_licenses_seq START WITH 1 INCREMENT BY 1 NOCACHE';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN
            RAISE;
        END IF;
END;
/

COMMIT;
