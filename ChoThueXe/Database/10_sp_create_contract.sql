-- ============================================================================
-- FILE: 10_sp_create_contract.sql
-- PURPOSE: Create draft contract procedure for employee/customer flow
-- NOTE: This procedure creates contract header only (no contract_details rows).
-- ============================================================================

SET SERVEROUTPUT ON;

create or replace procedure sp_create_contract(
    p_customer_id in number,
    p_employee_id in number
)
as
    v_customer_exists number := 0;
    v_employee_exists number := 0;
begin
    if p_customer_id is null or p_customer_id <= 0 then
        raise_application_error(-20001, 'CUSTOMER_ID khong hop le');
    end if;

    if p_employee_id is null or p_employee_id <= 0 then
        raise_application_error(-20002, 'EMPLOYEE_ID khong hop le');
    end if;

    select count(1)
      into v_customer_exists
      from users u
      join roles r on r.role_id = u.role_id
     where u.user_id = p_customer_id
       and upper(r.role_name) = 'CUSTOMER';

    if v_customer_exists = 0 then
        raise_application_error(-20003, 'Khong tim thay CUSTOMER hop le');
    end if;

    select count(1)
      into v_employee_exists
      from users u
      join roles r on r.role_id = u.role_id
     where u.user_id = p_employee_id
       and upper(r.role_name) in ('EMPLOYEE', 'ADMIN');

    if v_employee_exists = 0 then
        raise_application_error(-20004, 'Khong tim thay EMPLOYEE hop le');
    end if;

    insert into contracts (
        contract_id,
        customer_id,
        employee_id,
        contract_date,
        status,
        total_amount
    ) values (
        seq_contracts.nextval,
        p_customer_id,
        p_employee_id,
        sysdate,
        'PENDING',
        0
    );

exception
    when dup_val_on_index then
        raise_application_error(-20005, 'Trung khoa khi tao contract');
    when others then
        raise;
end;
/

show errors procedure sp_create_contract;

-- Grant execute so app user mapped to these roles can run the procedure.
grant execute on sp_create_contract to user_role;
grant execute on sp_create_contract to admin_role;

prompt '[OK] Created sp_create_contract and granted EXECUTE to user_role/admin_role';
