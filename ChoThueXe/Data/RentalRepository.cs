using ChoThueXe.Models.Rental;
using ChoThueXe.Models.Portal;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Text;

namespace ChoThueXe.Data;

public class RentalRepository : IRentalRepository
{
    private const string FavoriteVehiclesTable = "favorite_vehicles";
    private const string AmenitiesTable = "amenities";
    private const string VehicleAmenitiesTable = "vehicle_amenities";
    private const string SupportMessagesTable = "support_messages";
    private const string NotificationsTable = "notifications";
    private const string VehicleReviewsTable = "vehicle_reviews";
    private const string ProfileUpdateRequestsTable = "profile_update_requests";
    private const string ActivityLogsTable = "activity_logs";

    private static readonly IReadOnlyList<AmenityOptionViewModel> DefaultAmenityOptions =
    [
        new AmenityOptionViewModel { Code = "AIRBAG", Name = "Tui khi" },
        new AmenityOptionViewModel { Code = "SPACIOUS", Name = "Rong rai" },
        new AmenityOptionViewModel { Code = "FUEL_SAVING", Name = "Tiet kiem xang" },
        new AmenityOptionViewModel { Code = "REAR_CAMERA", Name = "Camera lui" },
        new AmenityOptionViewModel { Code = "BLUETOOTH", Name = "Bluetooth" },
        new AmenityOptionViewModel { Code = "GPS", Name = "GPS" }
    ];

    private readonly string _connectionString;

    public RentalRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Missing OracleDb connection string.");
    }

    public Task<IReadOnlyList<VehicleDetailViewModel>> GetVehiclesAsync(string? keyword = null, IReadOnlyCollection<string>? amenityCodes = null)
    {
        return GetVehiclesCoreAsync(null, keyword, amenityCodes);
    }

    public Task<IReadOnlyList<VehicleDetailViewModel>> GetVehiclesForCustomerAsync(int customerId, string? keyword = null, IReadOnlyCollection<string>? amenityCodes = null)
    {
        return GetVehiclesCoreAsync(customerId, keyword, amenityCodes);
    }

    public async Task<IReadOnlyList<VehicleDetailViewModel>> GetFavoriteVehiclesByCustomerAsync(int customerId)
    {
        var sql = $@"
            select
                v.vehicle_id,
                v.vehicle_name,
                v.brand_name,
                v.type_name,
                v.price_per_day,
                nvl((
                    select listagg(a.amenity_name, ', ') within group (order by a.amenity_name)
                    from {VehicleAmenitiesTable} va
                    join {AmenitiesTable} a on a.amenity_code = va.amenity_code
                    where va.vehicle_id = v.vehicle_id
                ), '') as amenities_text,
                nvl((
                    select vi.image_url
                    from vehicle_images vi
                    where vi.vehicle_id = v.vehicle_id
                    order by vi.image_id
                    fetch first 1 row only
                ), '') as primary_image_url,
                1 as is_favorite
            from vw_vehicle_detail v
            join {FavoriteVehiclesTable} fv on fv.vehicle_id = v.vehicle_id
            where fv.user_id = :p_user_id
            order by v.vehicle_id desc";

        var result = new List<VehicleDetailViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, customerId, ParameterDirection.Input);

        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new VehicleDetailViewModel
                {
                    VehicleId = Convert.ToInt32(reader["VEHICLE_ID"]),
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    BrandName = Convert.ToString(reader["BRAND_NAME"]) ?? string.Empty,
                    TypeName = Convert.ToString(reader["TYPE_NAME"]) ?? string.Empty,
                    PricePerDay = Convert.ToDecimal(reader["PRICE_PER_DAY"]),
                    AmenitiesText = Convert.ToString(reader["AMENITIES_TEXT"]) ?? string.Empty,
                    PrimaryImageUrl = Convert.ToString(reader["PRIMARY_IMAGE_URL"]) ?? string.Empty,
                    IsFavorite = true
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // favorite_vehicles table may not exist in early db provisioning.
            return [];
        }

        return result;
    }

    private async Task<IReadOnlyList<VehicleDetailViewModel>> GetVehiclesCoreAsync(int? customerId, string? keyword, IReadOnlyCollection<string>? amenityCodes)
    {
        var normalizedKeyword = string.IsNullOrWhiteSpace(keyword) ? string.Empty : keyword.Trim();
        var selectedAmenityCodes = (amenityCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<VehicleDetailViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        async Task ExecuteFallbackAsync(bool includeFavoriteJoin)
        {
            var fallbackSql = BuildVehicleFallbackSql(includeFavoriteJoin && customerId.HasValue, normalizedKeyword);
            await using var fallbackCommand = new OracleCommand(fallbackSql, connection);

            if (includeFavoriteJoin && customerId.HasValue)
            {
                fallbackCommand.Parameters.Add("p_user_id", OracleDbType.Int32, customerId.Value, ParameterDirection.Input);
            }

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                fallbackCommand.Parameters.Add("p_keyword", OracleDbType.Varchar2, normalizedKeyword.ToUpperInvariant(), ParameterDirection.Input);
            }

            await using var fallbackReader = await fallbackCommand.ExecuteReaderAsync();
            await FillVehicleListAsync(fallbackReader, result);
        }

        try
        {
            var sql = BuildVehicleSearchSql(customerId.HasValue, normalizedKeyword, selectedAmenityCodes);
            await using var command = new OracleCommand(sql, connection);

            if (customerId.HasValue)
            {
                command.Parameters.Add("p_user_id", OracleDbType.Int32, customerId.Value, ParameterDirection.Input);
            }

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                command.Parameters.Add("p_keyword", OracleDbType.Varchar2, normalizedKeyword.ToUpperInvariant(), ParameterDirection.Input);
            }

            for (var i = 0; i < selectedAmenityCodes.Count; i++)
            {
                command.Parameters.Add($"p_am_{i}", OracleDbType.Varchar2, selectedAmenityCodes[i], ParameterDirection.Input);
            }

            await using var reader = await command.ExecuteReaderAsync();
            await FillVehicleListAsync(reader, result);

            if (result.Count == 0)
            {
                try
                {
                    await ExecuteFallbackAsync(includeFavoriteJoin: true);
                }
                catch (OracleException ex) when (IsMissingObjectError(ex) && customerId.HasValue)
                {
                    await ExecuteFallbackAsync(includeFavoriteJoin: false);
                }
            }

            return result;
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            try
            {
                await ExecuteFallbackAsync(includeFavoriteJoin: true);
            }
            catch (OracleException fallbackEx) when (IsMissingObjectError(fallbackEx) && customerId.HasValue)
            {
                await ExecuteFallbackAsync(includeFavoriteJoin: false);
            }

            return result;
        }
    }

    private string BuildVehicleSearchSql(bool hasCustomer, string keyword, IReadOnlyList<string> selectedAmenityCodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("select");
        sb.AppendLine("    v.vehicle_id,");
        sb.AppendLine("    v.vehicle_name,");
        sb.AppendLine("    v.brand_name,");
        sb.AppendLine("    v.type_name,");
        sb.AppendLine("    v.price_per_day,");
        sb.AppendLine($"    nvl((select listagg(a.amenity_name, ', ') within group (order by a.amenity_name) from {VehicleAmenitiesTable} va join {AmenitiesTable} a on a.amenity_code = va.amenity_code where va.vehicle_id = v.vehicle_id), '') as amenities_text,");
        sb.AppendLine("    nvl((select vi.image_url from vehicle_images vi where vi.vehicle_id = v.vehicle_id order by vi.image_id fetch first 1 row only), '') as primary_image_url,");
        sb.AppendLine(hasCustomer
            ? "    case when fv.vehicle_id is not null then 1 else 0 end as is_favorite"
            : "    0 as is_favorite");
        sb.AppendLine("from vw_vehicle_detail v");

        if (hasCustomer)
        {
            sb.AppendLine($"left join {FavoriteVehiclesTable} fv on fv.vehicle_id = v.vehicle_id and fv.user_id = :p_user_id");
        }

        sb.AppendLine("where 1 = 1");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sb.AppendLine("  and instr(upper(v.vehicle_name || ' ' || v.brand_name || ' ' || v.type_name), :p_keyword) > 0");
        }

        for (var i = 0; i < selectedAmenityCodes.Count; i++)
        {
            sb.AppendLine($"  and exists (select 1 from {VehicleAmenitiesTable} va where va.vehicle_id = v.vehicle_id and upper(va.amenity_code) = :p_am_{i})");
        }

        sb.AppendLine("order by v.vehicle_id");
        return sb.ToString();
    }

    private string BuildVehicleFallbackSql(bool hasCustomer, string keyword)
    {
        var sb = new StringBuilder();
        sb.AppendLine("select");
        sb.AppendLine("    v.vehicle_id,");
        sb.AppendLine("    v.vehicle_name,");
        sb.AppendLine("    nvl(b.brand_name, '') as brand_name,");
        sb.AppendLine("    nvl(t.type_name, '') as type_name,");
        sb.AppendLine("    v.price_per_day,");
        sb.AppendLine("    '' as amenities_text,");
        sb.AppendLine("    nvl((select vi.image_url from vehicle_images vi where vi.vehicle_id = v.vehicle_id order by vi.image_id fetch first 1 row only), '') as primary_image_url,");
        sb.AppendLine(hasCustomer
            ? "    case when fv.vehicle_id is not null then 1 else 0 end as is_favorite"
            : "    0 as is_favorite");
        sb.AppendLine("from vehicles v");
        sb.AppendLine("left join brands b on b.brand_id = v.brand_id");
        sb.AppendLine("left join vehicle_types t on t.type_id = v.type_id");

        if (hasCustomer)
        {
            sb.AppendLine($"left join {FavoriteVehiclesTable} fv on fv.vehicle_id = v.vehicle_id and fv.user_id = :p_user_id");
        }

        sb.AppendLine("where 1 = 1");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sb.AppendLine("  and instr(upper(v.vehicle_name || ' ' || nvl(b.brand_name, '') || ' ' || nvl(t.type_name, '')), :p_keyword) > 0");
        }

        sb.AppendLine("order by v.vehicle_id");
        return sb.ToString();
    }

    private static async Task FillVehicleListAsync(OracleDataReader reader, List<VehicleDetailViewModel> result)
    {
        while (await reader.ReadAsync())
        {
            result.Add(new VehicleDetailViewModel
            {
                VehicleId = Convert.ToInt32(reader["VEHICLE_ID"]),
                VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                BrandName = Convert.ToString(reader["BRAND_NAME"]) ?? string.Empty,
                TypeName = Convert.ToString(reader["TYPE_NAME"]) ?? string.Empty,
                PricePerDay = Convert.ToDecimal(reader["PRICE_PER_DAY"]),
                AmenitiesText = Convert.ToString(reader["AMENITIES_TEXT"]) ?? string.Empty,
                PrimaryImageUrl = Convert.ToString(reader["PRIMARY_IMAGE_URL"]) ?? string.Empty,
                IsFavorite = Convert.ToInt32(reader["IS_FAVORITE"]) == 1
            });
        }
    }

    public async Task<IReadOnlyList<ContractFullViewModel>> GetContractsAsync()
    {
        const string sql = @"
            select contract_id, full_name, vehicle_name, start_date, end_date, total_amount, status
            from vw_contract_full
            order by contract_id desc";

        const string fallbackSql = @"
            select
                c.contract_id,
                u.full_name,
                nvl(v.vehicle_name, 'N/A') as vehicle_name,
                nvl(c.start_date, c.created_at) as start_date,
                nvl(c.end_date, c.created_at) as end_date,
                nvl(c.total_amount, 0) as total_amount,
                nvl(c.status, 'PENDING') as status
            from contracts c
            left join users u on u.user_id = c.customer_id
            left join vehicles v on v.vehicle_id = c.vehicle_id
            order by c.contract_id desc";

        const string legacyFallbackSql = @"
            select
                c.contract_id,
                u.full_name,
                nvl(v.vehicle_name, 'N/A') as vehicle_name,
                c.created_at as start_date,
                c.created_at as end_date,
                nvl(c.total_amount, 0) as total_amount,
                nvl(c.status, 'PENDING') as status
            from contracts c
            left join users u on u.user_id = c.customer_id
            left join vehicles v on v.vehicle_id = c.vehicle_id
            order by c.contract_id desc";

        var result = new List<ContractFullViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            await FillContractListAsync(reader, result);
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            try
            {
                await using var fallbackCommand = new OracleCommand(fallbackSql, connection);
                await using var fallbackReader = await fallbackCommand.ExecuteReaderAsync();
                await FillContractListAsync(fallbackReader, result);
            }
            catch (OracleException fallbackEx) when (fallbackEx.Number == 904)
            {
                await using var legacyFallbackCommand = new OracleCommand(legacyFallbackSql, connection);
                await using var legacyFallbackReader = await legacyFallbackCommand.ExecuteReaderAsync();
                await FillContractListAsync(legacyFallbackReader, result);
            }
            catch (OracleException fallbackEx) when (IsMissingObjectError(fallbackEx))
            {
                return [];
            }
        }

        return result;
    }

    private static async Task FillContractListAsync(OracleDataReader reader, List<ContractFullViewModel> result)
    {
        while (await reader.ReadAsync())
        {
            result.Add(new ContractFullViewModel
            {
                ContractId = Convert.ToInt32(reader["CONTRACT_ID"]),
                FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                StartDate = Convert.ToDateTime(reader["START_DATE"]),
                EndDate = Convert.ToDateTime(reader["END_DATE"]),
                TotalAmount = Convert.ToDecimal(reader["TOTAL_AMOUNT"]),
                Status = Convert.ToString(reader["STATUS"]) ?? string.Empty
            });
        }
    }

    public async Task<IReadOnlyList<RevenueViewModel>> GetRevenueAsync()
    {
        const string sql = @"
            select vehicle_name, total_revenue
            from vw_revenue
            order by total_revenue desc";

        var result = new List<RevenueViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new RevenueViewModel
                {
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    TotalRevenue = Convert.ToDecimal(reader["TOTAL_REVENUE"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<UserOptionViewModel>> GetUsersAsync()
    {
        const string sql = @"
            select user_id, full_name, email
            from users
            order by user_id";

        var result = new List<UserOptionViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new UserOptionViewModel
            {
                UserId = Convert.ToInt32(reader["USER_ID"]),
                FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                Email = Convert.ToString(reader["EMAIL"]) ?? string.Empty
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<UserOptionViewModel>> GetUsersByRoleAsync(string roleName)
    {
        const string sql = @"
            select u.user_id, u.full_name, u.email
            from users u
            join roles r on r.role_id = u.role_id
            where upper(r.role_name) = upper(:p_role_name)
            order by u.user_id";

        var result = new List<UserOptionViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_role_name", OracleDbType.Varchar2, roleName, ParameterDirection.Input);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new UserOptionViewModel
            {
                UserId = Convert.ToInt32(reader["USER_ID"]),
                FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                Email = Convert.ToString(reader["EMAIL"]) ?? string.Empty
            });
        }

        return result;
    }

    public async Task<(string FullName, string Email, string Phone)> GetUserProfileAsync(int userId)
    {
        const string sql = @"
            select full_name, email, nvl(phone, '') as phone
            from users
            where user_id = :p_user_id";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("User profile not found.");
        }

        return (
            Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
            Convert.ToString(reader["EMAIL"]) ?? string.Empty,
            Convert.ToString(reader["PHONE"]) ?? string.Empty
        );
    }

    public async Task<IReadOnlyList<PendingContractViewModel>> GetPendingContractsAsync()
    {
        const string sql = @"
            select
                c.contract_id,
                c.customer_id,
                u.full_name,
                nvl(c.total_amount, 0) as total_amount,
                nvl((
                    select sum(p.amount)
                    from payments p
                    where p.contract_id = c.contract_id
                ), 0) as paid_amount,
                c.status
            from contracts c
            join users u on u.user_id = c.customer_id
            where c.status = 'PENDING'
            order by c.contract_id desc";

        var result = new List<PendingContractViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new PendingContractViewModel
            {
                ContractId = Convert.ToInt32(reader["CONTRACT_ID"]),
                CustomerId = Convert.ToInt32(reader["CUSTOMER_ID"]),
                CustomerName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                TotalAmount = Convert.ToDecimal(reader["TOTAL_AMOUNT"] ?? 0m),
                PaidAmount = Convert.ToDecimal(reader["PAID_AMOUNT"] ?? 0m),
                Status = Convert.ToString(reader["STATUS"]) ?? string.Empty
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<PendingContractViewModel>> GetPendingContractsByCustomerAsync(int customerId)
    {
        const string sql = @"
            select
                c.contract_id,
                c.customer_id,
                u.full_name,
                c.total_amount,
                nvl((
                    select sum(p.amount)
                    from payments p
                    where p.contract_id = c.contract_id
                ), 0) as paid_amount,
                c.status
            from contracts c
            join users u on u.user_id = c.customer_id
            where c.status = 'PENDING'
              and c.customer_id = :p_customer_id
            order by c.contract_id desc";

        var result = new List<PendingContractViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_customer_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new PendingContractViewModel
            {
                ContractId = Convert.ToInt32(reader["CONTRACT_ID"]),
                CustomerId = Convert.ToInt32(reader["CUSTOMER_ID"]),
                CustomerName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                TotalAmount = Convert.ToDecimal(reader["TOTAL_AMOUNT"]),
                PaidAmount = Convert.ToDecimal(reader["PAID_AMOUNT"]),
                Status = Convert.ToString(reader["STATUS"]) ?? string.Empty
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ContractFullViewModel>> GetContractsByCustomerAsync(int customerId)
    {
        const string sql = @"
            select
                c.contract_id,
                u.full_name,
                nvl(v.vehicle_name, 'N/A') as vehicle_name,
                nvl(c.start_date, c.created_at) as start_date,
                nvl(c.end_date, c.created_at) as end_date,
                nvl(c.total_amount, 0) as total_amount,
                nvl(c.status, 'PENDING') as status
            from contracts c
            join users u on c.customer_id = u.user_id
            left join vehicles v on v.vehicle_id = c.vehicle_id
            where c.customer_id = :p_customer_id
            order by c.contract_id desc";

        const string fallbackSql = @"
            select
                c.contract_id,
                u.full_name,
                nvl(v.vehicle_name, 'N/A') as vehicle_name,
                c.created_at as start_date,
                c.created_at as end_date,
                nvl(c.total_amount, 0) as total_amount,
                nvl(c.status, 'PENDING') as status
            from contracts c
            join users u on c.customer_id = u.user_id
            left join vehicles v on v.vehicle_id = c.vehicle_id
            where c.customer_id = :p_customer_id
            order by c.contract_id desc";

        var result = new List<ContractFullViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_customer_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new ContractFullViewModel
                {
                    ContractId = Convert.ToInt32(reader["CONTRACT_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    StartDate = Convert.ToDateTime(reader["START_DATE"]),
                    EndDate = Convert.ToDateTime(reader["END_DATE"]),
                    TotalAmount = Convert.ToDecimal(reader["TOTAL_AMOUNT"]),
                    Status = Convert.ToString(reader["STATUS"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (ex.Number == 904)
        {
            await using var fallbackCommand = new OracleCommand(fallbackSql, connection);
            fallbackCommand.Parameters.Add("p_customer_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
            await using var reader = await fallbackCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new ContractFullViewModel
                {
                    ContractId = Convert.ToInt32(reader["CONTRACT_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    StartDate = Convert.ToDateTime(reader["START_DATE"]),
                    EndDate = Convert.ToDateTime(reader["END_DATE"]),
                    TotalAmount = Convert.ToDecimal(reader["TOTAL_AMOUNT"]),
                    Status = Convert.ToString(reader["STATUS"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<CustomerForEmployeeViewModel>> GetCustomersForEmployeeAsync()
    {
        const string sql = @"
            select u.user_id, u.full_name, u.email, fn_is_user_verified(u.user_id) as is_verified
            from users u
            join roles r on r.role_id = u.role_id
            where upper(r.role_name) = 'CUSTOMER'
            order by u.user_id";

        var result = new List<CustomerForEmployeeViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        
        try
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new CustomerForEmployeeViewModel
                {
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    Email = Convert.ToString(reader["EMAIL"]) ?? string.Empty,
                    IsVerified = Convert.ToInt32(reader["IS_VERIFIED"]) == 1
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // Function or table may not exist, return empty list
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<PendingDocumentViewModel>> GetPendingDocumentsAsync()
    {
        const string sql = @"
            select d.document_id, d.user_id, u.full_name, d.doc_type, nvl(d.file_url, '') as file_url, d.status
            from user_documents d
            join users u on u.user_id = d.user_id
            where d.status = 'PENDING'
            order by d.uploaded_at desc";

        var result = new List<PendingDocumentViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new PendingDocumentViewModel
                {
                    DocumentId = Convert.ToInt32(reader["DOCUMENT_ID"]),
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    DocType = Convert.ToString(reader["DOC_TYPE"] ) ?? string.Empty,
                    FileUrl = Convert.ToString(reader["FILE_URL"]) ?? string.Empty,
                    Status = Convert.ToString(reader["STATUS"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<PendingVerificationViewModel>> GetPendingVerificationsAsync()
    {
        const string sql = @"
            select
                d.user_id,
                u.full_name,
                max(case when upper(d.doc_type) = 'CCCD' then d.document_id end) as cccd_document_id,
                max(case when upper(d.doc_type) = 'CCCD' then nvl(d.file_url, '') end) as cccd_file_url,
                max(case when upper(d.doc_type) in ('DRIVER_LICENSE', 'DRIVER_LICENSES') then d.document_id end) as driver_license_document_id,
                max(case when upper(d.doc_type) in ('DRIVER_LICENSE', 'DRIVER_LICENSES') then nvl(d.file_url, '') end) as driver_license_file_url
            from user_documents d
            join users u on u.user_id = d.user_id
            where d.status = 'PENDING'
              and upper(d.doc_type) in ('CCCD', 'DRIVER_LICENSE', 'DRIVER_LICENSES')
            group by d.user_id, u.full_name
            having max(case when upper(d.doc_type) = 'CCCD' then 1 else 0 end) = 1
               and max(case when upper(d.doc_type) in ('DRIVER_LICENSE', 'DRIVER_LICENSES') then 1 else 0 end) = 1
            order by d.user_id";

        var result = new List<PendingVerificationViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new PendingVerificationViewModel
                {
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    CccdDocumentId = reader["CCCD_DOCUMENT_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["CCCD_DOCUMENT_ID"]),
                    CccdFileUrl = Convert.ToString(reader["CCCD_FILE_URL"]) ?? string.Empty,
                    DriverLicenseDocumentId = reader["DRIVER_LICENSE_DOCUMENT_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["DRIVER_LICENSE_DOCUMENT_ID"]),
                    DriverLicenseFileUrl = Convert.ToString(reader["DRIVER_LICENSE_FILE_URL"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<DriveLicenseViewModel>> GetDriveLicensesAsync(int userId)
    {
        const string sql = @"
            select drive_license_id, user_id, license_number, issued_by, issued_at, expire_at, created_at
            from drive_licenses
            where user_id = :p_user_id
            order by created_at desc";

        var result = new List<DriveLicenseViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new DriveLicenseViewModel
                {
                    DriveLicenseId = Convert.ToInt32(reader["DRIVE_LICENSE_ID"]),
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    LicenseNumber = Convert.ToString(reader["LICENSE_NUMBER"]) ?? string.Empty,
                    IssuedBy = Convert.ToString(reader["ISSUED_BY"]) ?? string.Empty,
                    IssuedAt = reader["ISSUED_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["ISSUED_AT"]),
                    ExpireAt = reader["EXPIRE_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["EXPIRE_AT"]),
                    CreatedAt = Convert.ToDateTime(reader["CREATED_AT"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task SubmitDriveLicenseAsync(int userId, string licenseNumber, DateTime issuedAt, DateTime expireAt, string issuedBy)
    {
        const string sql = @"
            insert into drive_licenses (
                drive_license_id, user_id, license_number, issued_by, issued_at, expire_at, created_at
            ) values (
                (select nvl(max(drive_license_id), 0) + 1 from drive_licenses),
                :p_user_id, :p_license_number, :p_issued_by, :p_issued_at, :p_expire_at, sysdate
            )";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
        command.Parameters.Add("p_license_number", OracleDbType.Varchar2, licenseNumber, ParameterDirection.Input);
        command.Parameters.Add("p_issued_by", OracleDbType.Varchar2, issuedBy, ParameterDirection.Input);
        command.Parameters.Add("p_issued_at", OracleDbType.Date, issuedAt, ParameterDirection.Input);
        command.Parameters.Add("p_expire_at", OracleDbType.Date, expireAt, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // Some environments do not provision drive_licenses yet.
            // Do not block customer flow; keep going with best-effort behavior.
        }

        await LogActivityAsync(userId, "SubmitDriveLicense", $"LicenseNumber={licenseNumber}, IssuedBy={issuedBy}");
    }

    public async Task<IReadOnlyList<BrandOptionViewModel>> GetBrandsAsync()
    {
        const string sql = "select brand_id, brand_name from brands order by brand_id";
        var result = new List<BrandOptionViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new BrandOptionViewModel
                {
                    BrandId = Convert.ToInt32(reader["BRAND_ID"]),
                    BrandName = Convert.ToString(reader["BRAND_NAME"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<TypeOptionViewModel>> GetTypesAsync()
    {
        const string sql = "select type_id, type_name from vehicle_types order by type_id";
        var result = new List<TypeOptionViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new TypeOptionViewModel
                {
                    TypeId = Convert.ToInt32(reader["TYPE_ID"]),
                    TypeName = Convert.ToString(reader["TYPE_NAME"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<AmenityOptionViewModel>> GetAmenityOptionsAsync()
    {
        const string sql = @"
            select amenity_code, amenity_name
            from amenities
            order by amenity_name";

        var result = new List<AmenityOptionViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AmenityOptionViewModel
                {
                    Code = Convert.ToString(reader["AMENITY_CODE"]) ?? string.Empty,
                    Name = Convert.ToString(reader["AMENITY_NAME"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return DefaultAmenityOptions;
        }

        return result.Count == 0 ? DefaultAmenityOptions : result;
    }

    public async Task<IReadOnlyList<NotificationViewModel>> GetNotificationsForUserAsync(int userId)
    {
        var sql = $@"
            select notification_id, user_id, title, message, nvl(is_read, 0) as is_read, created_at
            from {NotificationsTable}
            where user_id = :p_user_id
            order by created_at desc";

        var result = new List<NotificationViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new NotificationViewModel
                {
                    NotificationId = Convert.ToInt32(reader["NOTIFICATION_ID"]),
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    Title = Convert.ToString(reader["TITLE"]) ?? string.Empty,
                    Message = Convert.ToString(reader["MESSAGE"]) ?? string.Empty,
                    IsRead = Convert.ToInt32(reader["IS_READ"]) == 1,
                    CreatedAt = Convert.ToDateTime(reader["CREATED_AT"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<ReviewableContractViewModel>> GetReviewableContractsByCustomerAsync(int customerId)
    {
        var sql = $@"
            select c.contract_id, cd.vehicle_id, v.vehicle_name, c.end_date
            from (
                select c.contract_id, cd.vehicle_id, max(cd.end_date) as end_date
                from contracts c
                join contract_details cd on cd.contract_id = c.contract_id
                where c.customer_id = :p_customer_id
                  and upper(c.status) in ('COMPLETED', 'DONE', 'FINISHED', 'PAID')
                group by c.contract_id, cd.vehicle_id
            ) c
            join vehicles v on v.vehicle_id = c.vehicle_id
            left join {VehicleReviewsTable} r on r.contract_id = c.contract_id
            where r.contract_id is null
            order by c.end_date desc";

        var result = new List<ReviewableContractViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_customer_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new ReviewableContractViewModel
                {
                    ContractId = Convert.ToInt32(reader["CONTRACT_ID"]),
                    VehicleId = Convert.ToInt32(reader["VEHICLE_ID"]),
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    EndDate = Convert.ToDateTime(reader["END_DATE"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<AdminAccountManagementViewModel>> GetAdminAccountsAsync()
    {
        const string sql = @"
            select
                u.user_id,
                u.full_name,
                u.email,
                r.role_name,
                nvl((select count(1) from contracts c where c.customer_id = u.user_id), 0) as contract_count,
                nvl((
                    select sum(p.amount)
                    from contracts c
                    join payments p on p.contract_id = c.contract_id
                    where c.customer_id = u.user_id
                ), 0) as total_paid
            from users u
            join roles r on r.role_id = u.role_id
            order by u.user_id";

        var result = new List<AdminAccountManagementViewModel>();
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AdminAccountManagementViewModel
                {
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    Email = Convert.ToString(reader["EMAIL"]) ?? string.Empty,
                    RoleName = Convert.ToString(reader["ROLE_NAME"]) ?? string.Empty,
                    ContractCount = Convert.ToInt32(reader["CONTRACT_COUNT"]),
                    TotalPaid = Convert.ToDecimal(reader["TOTAL_PAID"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<AdminVehicleOccupancyViewModel>> GetAdminVehicleOccupanciesAsync()
    {
        const string sql = @"
            select
                v.vehicle_id,
                v.vehicle_name,
                nvl(v.status, 'UNKNOWN') as status,
                case
                    when exists (
                        select 1
                        from contract_details cd
                        join contracts c on c.contract_id = cd.contract_id
                        where cd.vehicle_id = v.vehicle_id
                          and upper(c.status) in ('PENDING', 'ACTIVE', 'IN_PROGRESS')
                    ) then 'DANG_THUE'
                    else 'XE_TRONG'
                end as occupancy
            from vehicles v
            order by v.vehicle_id";

        var result = new List<AdminVehicleOccupancyViewModel>();
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AdminVehicleOccupancyViewModel
                {
                    VehicleId = Convert.ToInt32(reader["VEHICLE_ID"]),
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    Status = Convert.ToString(reader["STATUS"]) ?? string.Empty,
                    Occupancy = Convert.ToString(reader["OCCUPANCY"]) ?? string.Empty
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<RevenueByAccountViewModel>> GetRevenueByAccountAsync()
    {
        const string sql = @"
            select
                u.user_id,
                u.full_name,
                nvl(sum(p.amount), 0) as total_revenue
            from users u
            left join contracts c on c.customer_id = u.user_id
            left join payments p on p.contract_id = c.contract_id
            group by u.user_id, u.full_name
            order by total_revenue desc, u.user_id";

        var result = new List<RevenueByAccountViewModel>();
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new RevenueByAccountViewModel
                {
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    FullName = Convert.ToString(reader["FULL_NAME"]) ?? string.Empty,
                    TotalRevenue = Convert.ToDecimal(reader["TOTAL_REVENUE"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<TopRentedVehicleViewModel>> GetTopRentedVehiclesAsync()
    {
        const string sql = @"
            select
                v.vehicle_id,
                v.vehicle_name,
                count(cd.contract_id) as rent_count
            from vehicles v
            left join contract_details cd on cd.vehicle_id = v.vehicle_id
            group by v.vehicle_id, v.vehicle_name
            order by rent_count desc, v.vehicle_id";

        var result = new List<TopRentedVehicleViewModel>();
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new TopRentedVehicleViewModel
                {
                    VehicleId = Convert.ToInt32(reader["VEHICLE_ID"]),
                    VehicleName = Convert.ToString(reader["VEHICLE_NAME"]) ?? string.Empty,
                    RentCount = Convert.ToInt32(reader["RENT_COUNT"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<SupportMessageViewModel>> GetMessagesForAdminAsync()
    {
        var sql = $@"
            select
                m.message_id,
                m.sender_id,
                su.full_name as sender_name,
                m.receiver_id,
                ru.full_name as receiver_name,
                m.content,
                nvl(m.reply_content, '') as reply_content,
                m.status,
                m.sent_at,
                m.replied_at
            from {SupportMessagesTable} m
            join users su on su.user_id = m.sender_id
            join users ru on ru.user_id = m.receiver_id
            order by m.sent_at desc";

        var result = new List<SupportMessageViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(MapMessage(reader));
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<IReadOnlyList<SupportMessageViewModel>> GetMessagesForCustomerAsync(int customerId)
    {
        var sql = $@"
            select
                m.message_id,
                m.sender_id,
                su.full_name as sender_name,
                m.receiver_id,
                ru.full_name as receiver_name,
                m.content,
                nvl(m.reply_content, '') as reply_content,
                m.status,
                m.sent_at,
                m.replied_at
            from {SupportMessagesTable} m
            join users su on su.user_id = m.sender_id
            join users ru on ru.user_id = m.receiver_id
            where m.sender_id = :p_customer_id or m.receiver_id = :p_customer_id
            order by m.sent_at desc";

        var result = new List<SupportMessageViewModel>();

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_customer_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(MapMessage(reader));
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task<bool> IsUserVerifiedAsync(int userId)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        const string block = "begin :result := fn_is_user_verified(:p_user_id); end;";
        await using var command = new OracleCommand(block, connection)
        {
            CommandType = CommandType.Text
        };

        var returnParameter = new OracleParameter("result", OracleDbType.Int32, ParameterDirection.ReturnValue);
        command.Parameters.Add(returnParameter);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex) || ex.Number == 6550)
        {
            return false;
        }

        var raw = returnParameter.Value;
        if (raw is OracleDecimal oracleDecimal)
        {
            return oracleDecimal.ToInt32() == 1;
        }

        return Convert.ToInt32(raw) == 1;
    }

    public async Task<CustomerVerificationStatusViewModel> GetCustomerVerificationStatusAsync(int userId)
    {
        const string sql = @"
            select
                nvl(max(case when upper(d.doc_type) = 'CCCD' then 1 else 0 end), 0) as has_cccd,
                max(case when upper(d.doc_type) = 'CCCD' then d.status end) as cccd_status,
                max(case when upper(d.doc_type) = 'CCCD' then d.document_id end) as cccd_document_id,
                nvl(max(case when upper(d.doc_type) in ('DRIVER_LICENSE', 'DRIVER_LICENSES') then 1 else 0 end), 0) as has_driver_license,
                max(case when upper(d.doc_type) in ('DRIVER_LICENSE', 'DRIVER_LICENSES') then d.status end) as driver_license_status,
                max(case when upper(d.doc_type) in ('DRIVER_LICENSE', 'DRIVER_LICENSES') then d.document_id end) as driver_license_document_id
            from user_documents d
            where d.user_id = :p_user_id
            group by d.user_id";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new CustomerVerificationStatusViewModel
                {
                    HasCccd = Convert.ToInt32(reader["HAS_CCCD"]) == 1,
                    CccdStatus = Convert.ToString(reader["CCCD_STATUS"]) ?? string.Empty,
                    CccdDocumentId = reader["CCCD_DOCUMENT_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["CCCD_DOCUMENT_ID"]),
                    HasDriverLicense = Convert.ToInt32(reader["HAS_DRIVER_LICENSE"]) == 1,
                    DriverLicenseStatus = Convert.ToString(reader["DRIVER_LICENSE_STATUS"]) ?? string.Empty,
                    DriverLicenseDocumentId = reader["DRIVER_LICENSE_DOCUMENT_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["DRIVER_LICENSE_DOCUMENT_ID"])
                };
            }

            return new CustomerVerificationStatusViewModel();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return new CustomerVerificationStatusViewModel();
        }
    }

    public async Task<decimal> CalculateRentalCostAsync(decimal pricePerDay, DateTime startDate, DateTime endDate)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        const string block = "begin :result := fn_calculate_rental_cost(:p_price_per_day, :p_start_date, :p_end_date); end;";
        await using var command = new OracleCommand(block, connection)
        {
            CommandType = CommandType.Text
        };

        var returnParameter = new OracleParameter("result", OracleDbType.Decimal, ParameterDirection.ReturnValue);
        command.Parameters.Add(returnParameter);
        command.Parameters.Add("p_price_per_day", OracleDbType.Decimal, pricePerDay, ParameterDirection.Input);
        command.Parameters.Add("p_start_date", OracleDbType.Date, startDate, ParameterDirection.Input);
        command.Parameters.Add("p_end_date", OracleDbType.Date, endDate, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex) || ex.Number == 6550)
        {
            var rentalDays = (int)Math.Ceiling((endDate.Date - startDate.Date).TotalDays);
            if (rentalDays <= 0)
            {
                rentalDays = 1;
            }

            return rentalDays * pricePerDay;
        }

        var raw = returnParameter.Value;
        if (raw is OracleDecimal oracleDecimal)
        {
            return oracleDecimal.Value;
        }

        return Convert.ToDecimal(raw);
    }

    public async Task UpdateUserProfileAsync(int userId, string fullName, string phone)
    {
        const string sql = @"
            update users
            set full_name = :p_full_name,
                phone = :p_phone
            where user_id = :p_user_id";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_full_name", OracleDbType.Varchar2, fullName, ParameterDirection.Input);
        command.Parameters.Add("p_phone", OracleDbType.Varchar2, phone, ParameterDirection.Input);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SubmitProfileUpdateRequestAsync(int userId, string fullName, string phone)
    {
        var sql = $@"
            insert into {ProfileUpdateRequestsTable} (
                request_id, user_id, requested_full_name, requested_phone, status, requested_at
            )
            values (
                :p_request_id, :p_user_id, :p_full_name, :p_phone, 'PENDING', sysdate
            )";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            var requestId = await GetNextIdNonTransactionalAsync(connection, ProfileUpdateRequestsTable, "request_id");
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_request_id", OracleDbType.Int32, requestId, ParameterDirection.Input);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            command.Parameters.Add("p_full_name", OracleDbType.Varchar2, fullName.Trim(), ParameterDirection.Input);
            command.Parameters.Add("p_phone", OracleDbType.Varchar2, phone.Trim(), ParameterDirection.Input);
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            throw new InvalidOperationException("Chua co bang profile_update_requests. Hay chay script DB moi.");
        }
    }

    public async Task<IReadOnlyList<PendingProfileUpdateRequestViewModel>> GetPendingProfileUpdateRequestsAsync()
    {
        var sql = $@"
            select
                r.request_id,
                r.user_id,
                nvl(u.full_name, '') as current_full_name,
                nvl(u.phone, '') as current_phone,
                nvl(r.requested_full_name, '') as requested_full_name,
                nvl(r.requested_phone, '') as requested_phone,
                r.requested_at
            from {ProfileUpdateRequestsTable} r
            join users u on u.user_id = r.user_id
            where r.status = 'PENDING'
            order by r.requested_at desc";

        var result = new List<PendingProfileUpdateRequestViewModel>();
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new PendingProfileUpdateRequestViewModel
                {
                    RequestId = Convert.ToInt32(reader["REQUEST_ID"]),
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    CurrentFullName = Convert.ToString(reader["CURRENT_FULL_NAME"]) ?? string.Empty,
                    CurrentPhone = Convert.ToString(reader["CURRENT_PHONE"]) ?? string.Empty,
                    RequestedFullName = Convert.ToString(reader["REQUESTED_FULL_NAME"]) ?? string.Empty,
                    RequestedPhone = Convert.ToString(reader["REQUESTED_PHONE"]) ?? string.Empty,
                    RequestedAt = Convert.ToDateTime(reader["REQUESTED_AT"])
                });
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            return [];
        }

        return result;
    }

    public async Task ReviewProfileUpdateRequestAsync(int requestId, int approvedBy, bool isApproved)
    {
        var sqlGet = $@"
            select user_id, requested_full_name, requested_phone
            from {ProfileUpdateRequestsTable}
            where request_id = :p_request_id and status = 'PENDING'";

        var sqlApproveUser = @"
            update users
            set full_name = :p_full_name,
                phone = :p_phone
            where user_id = :p_user_id";

        var sqlUpdateRequest = $@"
            update {ProfileUpdateRequestsTable}
            set status = :p_status,
                reviewed_by = :p_reviewed_by,
                reviewed_at = sysdate
            where request_id = :p_request_id";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        int userId;
        string requestedFullName;
        string requestedPhone;

        await using (var getCommand = new OracleCommand(sqlGet, connection))
        {
            getCommand.Transaction = transaction;
            getCommand.Parameters.Add("p_request_id", OracleDbType.Int32, requestId, ParameterDirection.Input);
            await using var reader = await getCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Yeu cau khong ton tai hoac da xu ly.");
            }

            userId = Convert.ToInt32(reader["USER_ID"]);
            requestedFullName = Convert.ToString(reader["REQUESTED_FULL_NAME"]) ?? string.Empty;
            requestedPhone = Convert.ToString(reader["REQUESTED_PHONE"]) ?? string.Empty;
        }

        if (isApproved)
        {
            await using var approveCommand = new OracleCommand(sqlApproveUser, connection);
            approveCommand.Transaction = transaction;
            approveCommand.Parameters.Add("p_full_name", OracleDbType.Varchar2, requestedFullName, ParameterDirection.Input);
            approveCommand.Parameters.Add("p_phone", OracleDbType.Varchar2, requestedPhone, ParameterDirection.Input);
            approveCommand.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            await approveCommand.ExecuteNonQueryAsync();
        }

        await using (var updateRequestCommand = new OracleCommand(sqlUpdateRequest, connection))
        {
            updateRequestCommand.Transaction = transaction;
            updateRequestCommand.Parameters.Add("p_status", OracleDbType.Varchar2, isApproved ? "APPROVED" : "REJECTED", ParameterDirection.Input);
            updateRequestCommand.Parameters.Add("p_reviewed_by", OracleDbType.Int32, approvedBy, ParameterDirection.Input);
            updateRequestCommand.Parameters.Add("p_request_id", OracleDbType.Int32, requestId, ParameterDirection.Input);
            await updateRequestCommand.ExecuteNonQueryAsync();
        }

        transaction.Commit();

        await AddNotificationSafeAsync(
            connection,
            userId,
            "Ket qua cap nhat thong tin",
            isApproved ? "Yeu cau sua thong tin cua ban da duoc duyet." : "Yeu cau sua thong tin cua ban da bi tu choi.");

        await LogActivityAsync(approvedBy, "ReviewProfileUpdateRequest", $"RequestId={requestId}, UserId={userId}, Approved={isApproved}");
    }

    public async Task SubmitUserDocumentAsync(int userId, SubmitDocumentInputModel input)
    {
        const string sql = @"
            insert into user_documents (document_id, user_id, doc_type, file_url, status, uploaded_at)
            values ((select nvl(max(document_id), 0) + 1 from user_documents), :p_user_id, :p_doc_type, :p_file_url, 'PENDING', sysdate)";

        var normalizedDocType = NormalizeDocType(input.DocType);

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
        command.Parameters.Add("p_doc_type", OracleDbType.Varchar2, normalizedDocType, ParameterDirection.Input);
        command.Parameters.Add("p_file_url", OracleDbType.Varchar2, input.FileUrl, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            throw new InvalidOperationException("Chua co bang giay to nguoi dung tren he thong. Hay cap nhat schema DB moi nhat.");
        }

        await LogActivityAsync(userId, "SubmitDocument", $"DocType={normalizedDocType}, Url={input.FileUrl}");
    }

    public async Task ApproveDocumentAsync(int documentId, int approvedBy)
    {
        const string findUserSql = @"
            select user_id
            from user_documents
            where document_id = :p_document_id";

        const string sql = @"
            update user_documents
            set status = 'APPROVED',
                approved_by = :p_approved_by,
                approved_at = sysdate
            where document_id = :p_document_id";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        int? userId = null;
        await using (var findCommand = new OracleCommand(findUserSql, connection))
        {
            findCommand.Parameters.Add("p_document_id", OracleDbType.Int32, documentId, ParameterDirection.Input);
            var rawUserId = await findCommand.ExecuteScalarAsync();
            if (rawUserId is not null && rawUserId != DBNull.Value)
            {
                userId = Convert.ToInt32(rawUserId);
            }
        }

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_approved_by", OracleDbType.Int32, approvedBy, ParameterDirection.Input);
        command.Parameters.Add("p_document_id", OracleDbType.Int32, documentId, ParameterDirection.Input);

        await command.ExecuteNonQueryAsync();

        if (userId.HasValue)
        {
            await AddNotificationSafeAsync(connection, userId.Value, "Duyet giay to", "Admin da duyet giay to cua ban.");
            await LogActivityAsync(userId.Value, "ApproveDocument", $"DocumentId={documentId}, ApprovedBy={approvedBy}");
        }
    }

    public async Task ReviewUserDocumentsAsync(int userId, int approvedBy, bool isMatched)
    {
        var status = isMatched ? "APPROVED" : "REJECTED";
        const string sql = @"
            update user_documents
            set status = :p_status,
                approved_by = :p_approved_by,
                approved_at = sysdate
            where user_id = :p_user_id
              and status = 'PENDING'
              and upper(doc_type) in ('CCCD', 'DRIVER_LICENSE', 'DRIVER_LICENSES')";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using (var command = new OracleCommand(sql, connection))
        {
            command.Parameters.Add("p_status", OracleDbType.Varchar2, status, ParameterDirection.Input);
            command.Parameters.Add("p_approved_by", OracleDbType.Int32, approvedBy, ParameterDirection.Input);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            await command.ExecuteNonQueryAsync();
        }

        var message = isMatched
            ? "CCCD va bang lai cua ban da duoc duyet, thong tin hop le."
            : "CCCD va bang lai chua khop, vui long upload lai giay to ro rang hon.";

        await AddNotificationSafeAsync(connection, userId, "Ket qua duyet giay to", message);
        await LogActivityAsync(approvedBy, "ReviewUserDocuments", $"UserId={userId}, IsMatched={isMatched}");
    }

    public async Task AddVehicleAsync(CreateVehicleInputModel input)
    {
        const string insertVehicleWithLicenseSql = @"
            insert into vehicles (
                vehicle_id, owner_id, brand_id, type_id, vehicle_name,
                license_plate, seats, transmission, fuel_type, price_per_day, status
            )
            values (
                :p_vehicle_id,
                :p_owner_id, :p_brand_id, :p_type_id, :p_vehicle_name,
                :p_license_plate, :p_seats, :p_transmission, :p_fuel_type, :p_price_per_day, :p_status
            )";

        const string insertVehicleLegacySql = @"
            insert into vehicles (
                vehicle_id, owner_id, brand_id, type_id, vehicle_name,
                seats, transmission, fuel_type, price_per_day, status
            )
            values (
                :p_vehicle_id,
                :p_owner_id, :p_brand_id, :p_type_id, :p_vehicle_name,
                :p_seats, :p_transmission, :p_fuel_type, :p_price_per_day, :p_status
            )";

        const string insertImageSql = @"
            insert into vehicle_images (
                image_id, vehicle_id, image_url
            )
            values (
                :p_image_id, :p_vehicle_id, :p_image_url
            )";

        var insertAmenitySql = $@"
            insert into {VehicleAmenitiesTable} (vehicle_id, amenity_code)
            values (:p_vehicle_id, :p_amenity_code)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var vehicleColumnLengths = await GetTableVarcharLengthsAsync(connection, transaction, "VEHICLES");
        var imageColumnLengths = await GetTableVarcharLengthsAsync(connection, transaction, "VEHICLE_IMAGES");

        var vehicleId = await GetNextIdAsync(connection, transaction, "vehicles", "vehicle_id");
        var inserted = false;

        for (var attempt = 0; attempt < 3 && !inserted; attempt++)
        {
            try
            {
                await using var command = new OracleCommand(insertVehicleWithLicenseSql, connection);
                command.Transaction = transaction;
                command.Parameters.Add("p_vehicle_id", OracleDbType.Int32, vehicleId, ParameterDirection.Input);
                command.Parameters.Add("p_owner_id", OracleDbType.Int32, input.OwnerId, ParameterDirection.Input);
                command.Parameters.Add("p_brand_id", OracleDbType.Int32, input.BrandId, ParameterDirection.Input);
                command.Parameters.Add("p_type_id", OracleDbType.Int32, input.TypeId, ParameterDirection.Input);
                command.Parameters.Add("p_vehicle_name", OracleDbType.Varchar2, TruncateToColumnLength(input.VehicleName, vehicleColumnLengths, "VEHICLE_NAME"), ParameterDirection.Input);
                command.Parameters.Add("p_license_plate", OracleDbType.Varchar2, TruncateToColumnLength(input.LicensePlate, vehicleColumnLengths, "LICENSE_PLATE"), ParameterDirection.Input);
                command.Parameters.Add("p_seats", OracleDbType.Int32, input.Seats, ParameterDirection.Input);
                command.Parameters.Add("p_transmission", OracleDbType.Varchar2, TruncateToColumnLength(input.Transmission, vehicleColumnLengths, "TRANSMISSION"), ParameterDirection.Input);
                command.Parameters.Add("p_fuel_type", OracleDbType.Varchar2, TruncateToColumnLength(input.FuelType, vehicleColumnLengths, "FUEL_TYPE"), ParameterDirection.Input);
                command.Parameters.Add("p_price_per_day", OracleDbType.Decimal, input.PricePerDay, ParameterDirection.Input);
                command.Parameters.Add("p_status", OracleDbType.Varchar2, TruncateToColumnLength(input.Status, vehicleColumnLengths, "STATUS"), ParameterDirection.Input);
                await command.ExecuteNonQueryAsync();
                inserted = true;
            }
            catch (OracleException ex) when (ex.Number == 904)
            {
                await using var legacyCommand = new OracleCommand(insertVehicleLegacySql, connection);
                legacyCommand.Transaction = transaction;
                legacyCommand.Parameters.Add("p_vehicle_id", OracleDbType.Int32, vehicleId, ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_owner_id", OracleDbType.Int32, input.OwnerId, ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_brand_id", OracleDbType.Int32, input.BrandId, ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_type_id", OracleDbType.Int32, input.TypeId, ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_vehicle_name", OracleDbType.Varchar2, TruncateToColumnLength(input.VehicleName, vehicleColumnLengths, "VEHICLE_NAME"), ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_seats", OracleDbType.Int32, input.Seats, ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_transmission", OracleDbType.Varchar2, TruncateToColumnLength(input.Transmission, vehicleColumnLengths, "TRANSMISSION"), ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_fuel_type", OracleDbType.Varchar2, TruncateToColumnLength(input.FuelType, vehicleColumnLengths, "FUEL_TYPE"), ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_price_per_day", OracleDbType.Decimal, input.PricePerDay, ParameterDirection.Input);
                legacyCommand.Parameters.Add("p_status", OracleDbType.Varchar2, TruncateToColumnLength(input.Status, vehicleColumnLengths, "STATUS"), ParameterDirection.Input);
                await legacyCommand.ExecuteNonQueryAsync();
                inserted = true;
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                vehicleId = await GetNextIdAsync(connection, transaction, "vehicles", "vehicle_id");
                if (attempt == 2)
                {
                    throw;
                }
            }
        }

        if (!inserted)
        {
            throw new InvalidOperationException("Khong the them xe do xung dot du lieu khi tao ma xe moi.");
        }

        var imageUrls = ParseImageUrls(input.ImageUrls);
        if (imageUrls.Count > 0)
        {
            try
            {
                var nextImageId = await GetNextIdAsync(connection, transaction, "vehicle_images", "image_id");

                for (var i = 0; i < imageUrls.Count; i++)
                {
                    await using var imageCommand = new OracleCommand(insertImageSql, connection);
                    imageCommand.Transaction = transaction;
                    imageCommand.Parameters.Add("p_image_id", OracleDbType.Int32, nextImageId + i, ParameterDirection.Input);
                    imageCommand.Parameters.Add("p_vehicle_id", OracleDbType.Int32, vehicleId, ParameterDirection.Input);
                    imageCommand.Parameters.Add("p_image_url", OracleDbType.Varchar2, TruncateToColumnLength(imageUrls[i], imageColumnLengths, "IMAGE_URL"), ParameterDirection.Input);
                    await imageCommand.ExecuteNonQueryAsync();
                }
            }
            catch (OracleException ex) when (IsMissingObjectError(ex))
            {
                // Allow vehicle creation even if vehicle_images table is not provisioned.
            }
        }

        var selectedAmenities = (input.SelectedAmenities ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedAmenities.Count > 0)
        {
            try
            {
                for (var i = 0; i < selectedAmenities.Count; i++)
                {
                    await using var amenityCommand = new OracleCommand(insertAmenitySql, connection);
                    amenityCommand.Transaction = transaction;
                    amenityCommand.Parameters.Add("p_vehicle_id", OracleDbType.Int32, vehicleId, ParameterDirection.Input);
                    amenityCommand.Parameters.Add("p_amenity_code", OracleDbType.Varchar2, selectedAmenities[i], ParameterDirection.Input);
                    await amenityCommand.ExecuteNonQueryAsync();
                }
            }
            catch (OracleException ex) when (IsMissingObjectError(ex))
            {
                // Allow vehicle creation to continue if amenity tables are not created yet.
            }
        }

        transaction.Commit();
        try
        {
            await BroadcastVehicleNotificationAsync(input.OwnerId, vehicleId, input.VehicleName);
        }
        catch
        {
            // Notification side-effect should not block vehicle creation.
        }

        try
        {
            await LogActivityAsync(input.OwnerId, "AddVehicle", $"VehicleId={vehicleId}, Name={input.VehicleName}, FuelType={input.FuelType}");
        }
        catch
        {
            // Activity log side-effect should not block vehicle creation.
        }
    }

    public async Task ToggleFavoriteVehicleAsync(int customerId, int vehicleId)
    {
        var checkSql = $@"
            select count(1)
            from {FavoriteVehiclesTable}
            where user_id = :p_user_id and vehicle_id = :p_vehicle_id";

        var deleteSql = $@"
            delete from {FavoriteVehiclesTable}
            where user_id = :p_user_id and vehicle_id = :p_vehicle_id";

        var insertSql = $@"
            insert into {FavoriteVehiclesTable} (user_id, vehicle_id)
            values (:p_user_id, :p_vehicle_id)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var checkCommand = new OracleCommand(checkSql, connection);
        checkCommand.Parameters.Add("p_user_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
        checkCommand.Parameters.Add("p_vehicle_id", OracleDbType.Int32, vehicleId, ParameterDirection.Input);

        var existsRaw = await checkCommand.ExecuteScalarAsync();
        var exists = Convert.ToInt32(existsRaw) > 0;

        await using var mutateCommand = new OracleCommand(exists ? deleteSql : insertSql, connection);
        mutateCommand.Parameters.Add("p_user_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
        mutateCommand.Parameters.Add("p_vehicle_id", OracleDbType.Int32, vehicleId, ParameterDirection.Input);

        try
        {
            await mutateCommand.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // Ignore missing favorites table in this deployment environment.
        }
    }

    public async Task SendMessageToAdminAsync(int customerId, string content)
    {
        const string sqlFindAdmin = @"
            select u.user_id
            from users u
            join roles r on r.role_id = u.role_id
            where upper(r.role_name) = 'ADMIN'
            order by u.user_id
            fetch first 1 row only";

        var insertSql = $@"
            insert into {SupportMessagesTable} (
                message_id, sender_id, receiver_id, content, status, sent_at
            )
            values (
                :p_message_id, :p_sender_id, :p_receiver_id, :p_content, 'PENDING', sysdate
            )";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        int adminId;
        await using (var adminCommand = new OracleCommand(sqlFindAdmin, connection))
        {
            adminCommand.Transaction = transaction;
            var rawAdmin = await adminCommand.ExecuteScalarAsync();
            if (rawAdmin is null || rawAdmin == DBNull.Value)
            {
                throw new InvalidOperationException("Khong tim thay tai khoan Admin de nhan tin.");
            }

            adminId = Convert.ToInt32(rawAdmin);
        }

        var messageId = await GetNextIdAsync(connection, transaction, SupportMessagesTable, "message_id");

        await using (var insertCommand = new OracleCommand(insertSql, connection))
        {
            insertCommand.Transaction = transaction;
            insertCommand.Parameters.Add("p_message_id", OracleDbType.Int32, messageId, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_sender_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_receiver_id", OracleDbType.Int32, adminId, ParameterDirection.Input);
            insertCommand.Parameters.Add("p_content", OracleDbType.Varchar2, content.Trim(), ParameterDirection.Input);
            await insertCommand.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    public async Task ReplyMessageAsync(int messageId, int adminId, string replyContent)
    {
        var senderSql = $@"
            select sender_id
            from {SupportMessagesTable}
            where message_id = :p_message_id";

        var sql = $@"
            update {SupportMessagesTable}
            set reply_content = :p_reply_content,
                status = 'ANSWERED',
                replied_by = :p_admin_id,
                replied_at = sysdate
            where message_id = :p_message_id";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        int? customerId = null;
        await using (var senderCommand = new OracleCommand(senderSql, connection))
        {
            senderCommand.Parameters.Add("p_message_id", OracleDbType.Int32, messageId, ParameterDirection.Input);
            var rawCustomer = await senderCommand.ExecuteScalarAsync();
            if (rawCustomer is not null && rawCustomer != DBNull.Value)
            {
                customerId = Convert.ToInt32(rawCustomer);
            }
        }

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("p_reply_content", OracleDbType.Varchar2, replyContent.Trim(), ParameterDirection.Input);
        command.Parameters.Add("p_admin_id", OracleDbType.Int32, adminId, ParameterDirection.Input);
        command.Parameters.Add("p_message_id", OracleDbType.Int32, messageId, ParameterDirection.Input);
        await command.ExecuteNonQueryAsync();

        if (customerId.HasValue)
        {
            await AddNotificationSafeAsync(connection, customerId.Value, "Phan hoi tu Admin", "Admin da phan hoi tin nhan cua ban.");
        }
    }

    public async Task BroadcastVehicleNotificationAsync(int adminId, int vehicleId, string vehicleName)
    {
        const string sql = @"
            select u.user_id
            from users u
            join roles r on r.role_id = u.role_id
            where upper(r.role_name) = 'CUSTOMER'";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var customerIds = new List<int>();
            while (await reader.ReadAsync())
            {
                customerIds.Add(Convert.ToInt32(reader["USER_ID"]));
            }

            foreach (var customerId in customerIds)
            {
                await AddNotificationSafeAsync(
                    connection,
                    customerId,
                    "Xe moi duoc duyet",
                    $"Admin vua duyet xe #{vehicleId} - {vehicleName}.");
            }
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // Ignore if notifications table does not exist yet.
        }
    }

    public async Task AddVehicleReviewAsync(int customerId, VehicleReviewInputModel input)
    {
        var sql = $@"
            insert into {VehicleReviewsTable} (
                review_id, contract_id, vehicle_id, user_id, rating, comment, created_at
            )
            select
                :p_review_id,
                c.contract_id,
                cd.vehicle_id,
                :p_user_id,
                :p_rating,
                :p_comment,
                sysdate
            from contracts c
            join contract_details cd on cd.contract_id = c.contract_id
            where c.contract_id = :p_contract_id
              and c.customer_id = :p_user_id
              and upper(c.status) in ('COMPLETED', 'DONE', 'FINISHED', 'PAID')
              and not exists (
                    select 1
                    from {VehicleReviewsTable} r
                    where r.contract_id = c.contract_id
              )";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        var nextReviewId = await GetNextIdAsync(connection, transaction, VehicleReviewsTable, "review_id");

        await using var command = new OracleCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.Add("p_review_id", OracleDbType.Int32, nextReviewId, ParameterDirection.Input);
        command.Parameters.Add("p_user_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
        command.Parameters.Add("p_rating", OracleDbType.Int32, input.Rating, ParameterDirection.Input);
        command.Parameters.Add("p_comment", OracleDbType.Varchar2, input.Comment?.Trim() ?? string.Empty, ParameterDirection.Input);
        command.Parameters.Add("p_contract_id", OracleDbType.Int32, input.ContractId, ParameterDirection.Input);

        var rows = await command.ExecuteNonQueryAsync();
        if (rows <= 0)
        {
            throw new InvalidOperationException("Hop dong nay chua hoan tat hoac da duoc review.");
        }

        transaction.Commit();
    }

    public async Task CreateContractDraftAsync(int customerId, int employeeId)
    {
        if (customerId <= 0 || employeeId <= 0)
        {
            throw new InvalidOperationException("Thong tin tao hop dong khong hop le.");
        }

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand("sp_create_contract", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_customer_id", OracleDbType.Int32, customerId, ParameterDirection.Input);
        command.Parameters.Add("p_employee_id", OracleDbType.Int32, employeeId, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex) || ex.Number == 6550)
        {
            throw new InvalidOperationException("Khong the tao hop dong nhap vi stored procedure chua san sang tren DB.");
        }
    }

    public async Task RentVehicleAsync(RentVehicleInputModel input)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand("sp_rent_vehicle", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_customer_id", OracleDbType.Int32, input.CustomerId, ParameterDirection.Input);
        command.Parameters.Add("p_employee_id", OracleDbType.Int32, input.EmployeeId, ParameterDirection.Input);
        command.Parameters.Add("p_vehicle_id", OracleDbType.Int32, input.VehicleId, ParameterDirection.Input);
        command.Parameters.Add("p_start_date", OracleDbType.Date, input.StartDate, ParameterDirection.Input);
        command.Parameters.Add("p_end_date", OracleDbType.Date, input.EndDate, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex) || ex.Number == 6550)
        {
            throw new InvalidOperationException("Khong the tao chi tiet thue xe vi stored procedure chua san sang tren DB.");
        }
    }

    public async Task MakePaymentAsync(PaymentInputModel input)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand("sp_make_payment", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_contract_id", OracleDbType.Int32, input.ContractId, ParameterDirection.Input);
        command.Parameters.Add("p_amount", OracleDbType.Decimal, input.Amount, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex) || ex.Number == 6550)
        {
            throw new InvalidOperationException("Khong the thanh toan vi stored procedure chua san sang tren DB.");
        }
    }

    private static SupportMessageViewModel MapMessage(OracleDataReader reader)
    {
        return new SupportMessageViewModel
        {
            MessageId = Convert.ToInt32(reader["MESSAGE_ID"]),
            SenderId = Convert.ToInt32(reader["SENDER_ID"]),
            SenderName = Convert.ToString(reader["SENDER_NAME"]) ?? string.Empty,
            ReceiverId = Convert.ToInt32(reader["RECEIVER_ID"]),
            ReceiverName = Convert.ToString(reader["RECEIVER_NAME"]) ?? string.Empty,
            Content = Convert.ToString(reader["CONTENT"]) ?? string.Empty,
            ReplyContent = Convert.ToString(reader["REPLY_CONTENT"]) ?? string.Empty,
            Status = Convert.ToString(reader["STATUS"]) ?? string.Empty,
            SentAt = Convert.ToDateTime(reader["SENT_AT"]),
            RepliedAt = reader["REPLIED_AT"] == DBNull.Value ? null : Convert.ToDateTime(reader["REPLIED_AT"])
        };
    }

    public async Task LogActivityAsync(int? userId, string action, string details)
    {
        var sql = $@"
            insert into {ActivityLogsTable} (activity_id, user_id, action, details, created_at)
            values ((select nvl(max(activity_id), 0) + 1 from {ActivityLogsTable}), :p_user_id, :p_action, :p_details, sysdate)";

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId.HasValue ? userId.Value : (object?)null, ParameterDirection.Input);
            command.Parameters.Add("p_action", OracleDbType.Varchar2, action, ParameterDirection.Input);
            command.Parameters.Add("p_details", OracleDbType.Varchar2, details, ParameterDirection.Input);
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // Activity logs table may not exist yet in this environment.
        }
    }

    private static bool IsMissingObjectError(OracleException exception)
    {
        return exception.Number is 904 or 942;
    }

    private async Task AddNotificationSafeAsync(OracleConnection connection, int userId, string title, string message)
    {
        var insertSql = $@"
            insert into {NotificationsTable} (
                notification_id, user_id, title, message, is_read, created_at
            )
            values (
                :p_notification_id, :p_user_id, :p_title, :p_message, 0, sysdate
            )";

        try
        {
            var notificationId = await GetNextIdNonTransactionalAsync(connection, NotificationsTable, "notification_id");

            await using var command = new OracleCommand(insertSql, connection);
            command.Parameters.Add("p_notification_id", OracleDbType.Int32, notificationId, ParameterDirection.Input);
            command.Parameters.Add("p_user_id", OracleDbType.Int32, userId, ParameterDirection.Input);
            command.Parameters.Add("p_title", OracleDbType.Varchar2, title, ParameterDirection.Input);
            command.Parameters.Add("p_message", OracleDbType.Varchar2, message, ParameterDirection.Input);
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex))
        {
            // Ignore if notifications table is not present.
        }
    }

    private static string NormalizeDocType(string rawDocType)
    {
        var value = rawDocType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (value is "DRIVER_LICENSE" or "DRIVER_LICENSES" or "DRIVING_LICENSE" or "GPLX" or "BANG_LAI" or "BANG_LAI_XE")
        {
            return "DRIVER_LICENSES";
        }

        return value == "CCCD" ? "CCCD" : value;
    }

    private static List<string> ParseImageUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<Dictionary<string, int>> GetTableVarcharLengthsAsync(OracleConnection connection, OracleTransaction transaction, string tableName)
    {
        const string sql = @"
            select upper(column_name) as column_name, data_length
            from user_tab_columns
            where upper(table_name) = :p_table_name
              and data_type in ('VARCHAR2', 'NVARCHAR2', 'CHAR', 'NCHAR')";

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var command = new OracleCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.Add("p_table_name", OracleDbType.Varchar2, tableName.ToUpperInvariant(), ParameterDirection.Input);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnName = Convert.ToString(reader["COLUMN_NAME"]) ?? string.Empty;
            var length = Convert.ToInt32(reader["DATA_LENGTH"]);
            if (!string.IsNullOrWhiteSpace(columnName) && length > 0)
            {
                result[columnName] = length;
            }
        }

        return result;
    }

    private static string TruncateToColumnLength(string? value, IReadOnlyDictionary<string, int> maxLengths, string columnName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (!maxLengths.TryGetValue(columnName, out var maxLength) || maxLength <= 0)
        {
            return normalized;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static async Task<int> GetNextIdAsync(OracleConnection connection, OracleTransaction transaction, string tableName, string idColumn)
    {
        var sql = $"select nvl(max({idColumn}), 0) + 1 from {tableName}";
        await using var command = new OracleCommand(sql, connection);
        command.Transaction = transaction;
        var raw = await command.ExecuteScalarAsync();
        return Convert.ToInt32(raw);
    }

    private static async Task<int> GetNextIdNonTransactionalAsync(OracleConnection connection, string tableName, string idColumn)
    {
        var sql = $"select nvl(max({idColumn}), 0) + 1 from {tableName}";
        await using var command = new OracleCommand(sql, connection);
        var raw = await command.ExecuteScalarAsync();
        return Convert.ToInt32(raw);
    }

    public async Task<ContractFullViewModel?> GetContractByIdAsync(int contractId)
    {
        try
        {
            await using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            var sql = """
                select c.contract_id, c.customer_id, c.employee_id, c.vehicle_id,
                       c.start_date, c.end_date, c.total_amount, c.status, c.created_at,
                       u.full_name as customer_name, u.email as customer_email,
                       e.full_name as employee_name, e.email as employee_email,
                       v.vehicle_name, v.price_per_day, b.brand_name, t.type_name
                from contracts c
                left join users u on c.customer_id = u.user_id
                left join users e on c.employee_id = e.user_id
                left join vehicles v on c.vehicle_id = v.vehicle_id
                left join brands b on v.brand_id = b.brand_id
                left join vehicle_types t on v.type_id = t.type_id
                where c.contract_id = :contract_id
                """;

            await using var command = new OracleCommand(sql, connection);
            command.Parameters.Add("contract_id", OracleDbType.Int32, contractId, ParameterDirection.Input);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new ContractFullViewModel
            {
                ContractId = reader.GetInt32(0),
                CustomerId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                EmployeeId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                VehicleId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                StartDate = reader.GetDateTime(4),
                EndDate = reader.GetDateTime(5),
                TotalAmount = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                Status = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                CreatedAt = reader.GetDateTime(8),
                CustomerName = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                CustomerEmail = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                EmployeeName = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                EmployeeEmail = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                VehicleName = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                PricePerDay = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14),
                BrandName = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                TypeName = reader.IsDBNull(16) ? string.Empty : reader.GetString(16)
            };
        }
        catch (OracleException)
        {
            return null;
        }
    }

    public async Task UpdateVehicleAsync(CreateVehicleInputModel input)
    {
        if (input.VehicleId <= 0)
        {
            throw new ArgumentException("Vehicle ID must be greater than 0.");
        }

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        var sql = """
            update vehicles set
                vehicle_name = :vehicle_name,
                brand_id = :brand_id,
                type_id = :type_id,
                license_plate = :license_plate,
                price_per_day = :price_per_day,
                status = :status
            where vehicle_id = :vehicle_id
            """;

        await using var command = new OracleCommand(sql, connection);
        command.Parameters.Add("vehicle_id", OracleDbType.Int32, input.VehicleId, ParameterDirection.Input);
        command.Parameters.Add("vehicle_name", OracleDbType.Varchar2, input.VehicleName, ParameterDirection.Input);
        command.Parameters.Add("brand_id", OracleDbType.Int32, input.BrandId, ParameterDirection.Input);
        command.Parameters.Add("type_id", OracleDbType.Int32, input.TypeId, ParameterDirection.Input);
        command.Parameters.Add("license_plate", OracleDbType.Varchar2, input.LicensePlate, ParameterDirection.Input);
        command.Parameters.Add("price_per_day", OracleDbType.Decimal, input.PricePerDay, ParameterDirection.Input);
        command.Parameters.Add("status", OracleDbType.Varchar2, input.Status ?? "ACTIVE", ParameterDirection.Input);

        await command.ExecuteNonQueryAsync();

        await LogActivityAsync(input.OwnerId, "UPDATE_VEHICLE", $"Updated vehicle {input.VehicleId}: {input.VehicleName}");
    }

    public async Task ApproveContractAsync(int contractId)
    {
        if (contractId <= 0)
        {
            throw new InvalidOperationException("Hop dong khong hop le.");
        }

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new OracleCommand("sp_approve_contract", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("p_contract_id", OracleDbType.Int32, contractId, ParameterDirection.Input);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (IsMissingObjectError(ex) || ex.Number == 6550)
        {
            const string fallbackSql = @"
                update contracts
                set status = 'ACTIVE'
                where contract_id = :p_contract_id";

            await using var fallbackCommand = new OracleCommand(fallbackSql, connection);
            fallbackCommand.Parameters.Add("p_contract_id", OracleDbType.Int32, contractId, ParameterDirection.Input);
            var affected = await fallbackCommand.ExecuteNonQueryAsync();

            if (affected <= 0)
            {
                throw new InvalidOperationException("Khong tim thay hop dong de duyet.");
            }
        }
    }
}

