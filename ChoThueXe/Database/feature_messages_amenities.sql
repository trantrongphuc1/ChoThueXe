-- Run this script once in Oracle to enable amenities and customer-admin messaging.

begin
    execute immediate '
        create table amenities (
            amenity_code varchar2(50) primary key,
            amenity_name varchar2(100) not null
        )';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate '
        create table vehicle_amenities (
            vehicle_id number not null,
            amenity_code varchar2(50) not null,
            constraint pk_vehicle_amenities primary key (vehicle_id, amenity_code),
            constraint fk_va_vehicle foreign key (vehicle_id) references vehicles(vehicle_id),
            constraint fk_va_amenity foreign key (amenity_code) references amenities(amenity_code)
        )';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate '
        create table support_messages (
            message_id number primary key,
            sender_id number not null,
            receiver_id number not null,
            content varchar2(1000) not null,
            reply_content varchar2(1000),
            status varchar2(30) default ''PENDING'' not null,
            sent_at date default sysdate not null,
            replied_by number,
            replied_at date,
            constraint fk_sm_sender foreign key (sender_id) references users(user_id),
            constraint fk_sm_receiver foreign key (receiver_id) references users(user_id),
            constraint fk_sm_replied_by foreign key (replied_by) references users(user_id)
        )';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate 'create index idx_sm_receiver_status on support_messages(receiver_id, status)';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate '
        create table notifications (
            notification_id number primary key,
            user_id number not null,
            title varchar2(200) not null,
            message varchar2(1000) not null,
            is_read number(1) default 0 not null,
            created_at date default sysdate not null,
            constraint fk_noti_user foreign key (user_id) references users(user_id)
        )';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate 'create index idx_notifications_user on notifications(user_id, created_at desc)';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate '
        create table vehicle_reviews (
            review_id number primary key,
            contract_id number not null,
            vehicle_id number not null,
            user_id number not null,
            rating number(1) not null,
            comment varchar2(1000),
            created_at date default sysdate not null,
            constraint chk_vehicle_reviews_rating check (rating between 1 and 5),
            constraint uk_vehicle_reviews_contract unique (contract_id),
            constraint fk_vehicle_reviews_contract foreign key (contract_id) references contracts(contract_id),
            constraint fk_vehicle_reviews_vehicle foreign key (vehicle_id) references vehicles(vehicle_id),
            constraint fk_vehicle_reviews_user foreign key (user_id) references users(user_id)
        )';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate '
        create table profile_update_requests (
            request_id number primary key,
            user_id number not null,
            requested_full_name varchar2(200) not null,
            requested_phone varchar2(20),
            status varchar2(30) default ''PENDING'' not null,
            requested_at date default sysdate not null,
            reviewed_by number,
            reviewed_at date,
            constraint fk_pur_user foreign key (user_id) references users(user_id),
            constraint fk_pur_reviewed_by foreign key (reviewed_by) references users(user_id)
        )';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

begin
    execute immediate 'create index idx_pur_status_time on profile_update_requests(status, requested_at desc)';
exception
    when others then
        if sqlcode != -955 then
            raise;
        end if;
end;
/

merge into amenities a
using (
    select 'AIRBAG' as amenity_code, 'Tui khi' as amenity_name from dual union all
    select 'SPACIOUS', 'Rong rai' from dual union all
    select 'FUEL_SAVING', 'Tiet kiem xang' from dual union all
    select 'REAR_CAMERA', 'Camera lui' from dual union all
    select 'BLUETOOTH', 'Bluetooth' from dual union all
    select 'GPS', 'GPS' from dual
) src
on (a.amenity_code = src.amenity_code)
when matched then
    update set a.amenity_name = src.amenity_name
when not matched then
    insert (amenity_code, amenity_name)
    values (src.amenity_code, src.amenity_name);
/
