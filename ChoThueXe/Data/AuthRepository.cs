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

    private static async Task<int> GetNextUserIdAsync(OracleConnection connection, OracleTransaction transaction)
    {
        const string sql = "select nvl(max(user_id), 0) + 1 from users";
        await using var command = new OracleCommand(sql, connection);
        command.Transaction = transaction;
        var raw = await command.ExecuteScalarAsync();
        return Convert.ToInt32(raw);
    }
}
