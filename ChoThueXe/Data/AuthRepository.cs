using ChoThueXe.Models.Auth;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace ChoThueXe.Data;

public class AuthRepository : IAuthRepository
{
    private readonly string _connectionString;

    public AuthRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Missing OracleDb connection string.");
    }

    public async Task<AuthenticatedUserViewModel?> AuthenticateAsync(string email, string password)
    {
        const string sql = @"
            select u.user_id, u.full_name, u.email, r.role_name
            from users u
            join roles r on r.role_id = u.role_id
            where lower(u.email) = lower(:p_email)
              and u.password = :p_password";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_email", OracleDbType.Varchar2, email, System.Data.ParameterDirection.Input);
        command.Parameters.Add("p_password", OracleDbType.Varchar2, password, System.Data.ParameterDirection.Input);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new AuthenticatedUserViewModel
        {
            UserId = Convert.ToInt32(reader["USER_ID"]),
            FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
            Email = Convert.ToString(reader["EMAIL"]) ?? string.Empty,
            RoleName = Convert.ToString(reader["ROLE_NAME"]) ?? string.Empty
        };
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        const string sql = @"
            select count(1)
            from users
            where lower(email) = lower(:p_email)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);

        var raw = await command.ExecuteScalarAsync();
        return Convert.ToInt32(raw) > 0;
    }

    public async Task RegisterCustomerAsync(RegisterInputModel input)
    {
        const string roleSql = @"
            select role_id
            from roles
            where upper(role_name) = 'CUSTOMER'
            fetch first 1 row only";

        const string insertSql = @"
            insert into users (user_id, role_id, full_name, email, password, phone)
            values (:p_user_id, :p_role_id, :p_full_name, :p_email, :p_password, :p_phone)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        int roleId;
        await using (var roleCommand = new OracleCommand(roleSql, connection))
        {
            roleCommand.Transaction = transaction;
            var rawRoleId = await roleCommand.ExecuteScalarAsync();
            if (rawRoleId is null || rawRoleId == DBNull.Value)
            {
                throw new InvalidOperationException("Khong tim thay role CUSTOMER.");
            }

            roleId = Convert.ToInt32(rawRoleId);
        }

        var userId = await GetNextUserIdAsync(connection, transaction);

        await using (var insertCommand = new OracleCommand(insertSql, connection))
        {
            insertCommand.Transaction = transaction;
            insertCommand.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_role_id", OracleDbType.Int32, roleId, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_full_name", OracleDbType.Varchar2, input.FullName.Trim(), ParameterDirection.Input);
            insertCommand.Parameters.Add("p_email", OracleDbType.Varchar2, input.Email.Trim(), ParameterDirection.Input);
            insertCommand.Parameters.Add("p_password", OracleDbType.Varchar2, input.Password, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_phone", OracleDbType.Varchar2, input.Phone?.Trim() ?? string.Empty, ParameterDirection.Input);
            await insertCommand.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    public async Task<string> GenerateOtpAsync(string email)
    {
        const string selectUserSql = @"
            select user_id
            from users
            where lower(email) = lower(:p_email)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        int? userId = null;
        await using (var selectCommand = new OracleCommand(selectUserSql, connection))
        {
            selectCommand.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);
            var raw = await selectCommand.ExecuteScalarAsync();
            if (raw is not null && raw != DBNull.Value)
            {
                userId = Convert.ToInt32(raw);
            }
            else
            {
                throw new InvalidOperationException("Email khong ton tai.");
            }
        }

        var otpCode = new Random().Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        const string insertSql = @"
            insert into otp_codes (otp_id, user_id, email, otp_code, expires_at, is_used, created_at)
            values ((select nvl(max(otp_id), 0) + 1 from otp_codes), :p_user_id, :p_email, :p_otp_code, :p_expires_at, 0, sysdate)";

        await using (var insertCommand = new OracleCommand(insertSql, connection))
        {
            insertCommand.Parameters.Add("p_user_id", OracleDbType.Int32, userId.Value, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_otp_code", OracleDbType.Varchar2, otpCode, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_expires_at", OracleDbType.Date, expiresAt, ParameterDirection.Input);
            await insertCommand.ExecuteNonQueryAsync();
        }

        return otpCode;
    }

    public async Task<bool> ValidateOtpAsync(string email, string otpCode)
    {
        const string selectSql = @"
            select otp_id
            from otp_codes
            where lower(email) = lower(:p_email)
              and otp_code = :p_otp_code
              and is_used = 0
              and expires_at >= sysdate";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        object? otpId;
        await using (var selectCommand = new OracleCommand(selectSql, connection))
        {
            selectCommand.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);
            selectCommand.Parameters.Add("p_otp_code", OracleDbType.Varchar2, otpCode, ParameterDirection.Input);
            otpId = await selectCommand.ExecuteScalarAsync();
        }

        if (otpId is null || otpId == DBNull.Value)
        {
            return false;
        }

        const string updateSql = @"
            update otp_codes
            set is_used = 1
            where otp_id = :p_otp_id";

        await using var updateCommand = new OracleCommand(updateSql, connection);
        updateCommand.Parameters.Add("p_otp_id", OracleDbType.Int32, Convert.ToInt32(otpId), ParameterDirection.Input);
        await updateCommand.ExecuteNonQueryAsync();

        return true;
    }

    public async Task ResetPasswordAsync(string email, string newPassword)
    {
        const string sql = @"
            update users
            set password = :p_password
            where lower(email) = lower(:p_email)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_password", OracleDbType.Varchar2, newPassword, ParameterDirection.Input);
        command.Parameters.Add("p_email", OracleDbType.Varchar2, email, ParameterDirection.Input);

        var affected = await command.ExecuteNonQueryAsync();
        if (affected == 0)
        {
            throw new InvalidOperationException("Email khong ton tai.");
        }
    }

    private static async Task<int> GetNextUserIdAsync(OracleConnection connection, OracleTransaction transaction)
    {
        const string sql = "select nvl(max(user_id), 0) + 1 from users";
        await using var command = new OracleCommand(sql, connection);
        command.Transaction = transaction;
        var raw = await command.ExecuteScalarAsync();
        return Convert.ToInt32(raw);
    }
}
