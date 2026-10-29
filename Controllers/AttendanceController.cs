
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ngohoangduc.Models;

namespace ngohoangduc.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;

    public AttendanceController(AppDbContext db)
    {
        _db = db;
    }

    private string? UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    private string UserName =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? User.FindFirstValue(ClaimTypes.Email)
        ?? UserId
        ?? "Nhan vien";

    private NpgsqlConnection Connection =>
        (NpgsqlConnection)_db.Database.GetDbConnection();

    private static NpgsqlCommand Command(
        NpgsqlConnection connection,
        string statement,
        NpgsqlTransaction? transaction = null)
    {
        return new NpgsqlCommand(statement, connection, transaction);
    }

    // POST /api/attendance/qr
    // Chi Admin duoc tao ma QR.
    [HttpPost("qr")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateQr()
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        string token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32)
        );

        DateTime expiresAt = DateTime.UtcNow.AddMinutes(2);

        var connection = Connection;
        await connection.OpenAsync();

        try
        {
            await using var cmd = Command(
                connection,
                """
                INSERT INTO dbo."AttendanceQrCodes"
                ("Token", "CreatedBy", "ExpiresAt")
                VALUES (@token, @userId, @expiresAt)
                """
            );

            cmd.Parameters.AddWithValue("token", token);
            cmd.Parameters.AddWithValue("userId", UserId!);
            cmd.Parameters.AddWithValue("expiresAt", expiresAt);

            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                token,
                expiresAt,
                expiresInSeconds = 120
            });
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    public sealed class ScanQrRequest
    {
        public string Token { get; set; } = "";
    }

    // POST /api/attendance/scan
    // Chi nhan vien duoc quet de cham cong.
    [HttpPost("scan")]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> ScanQr(
        [FromBody] ScanQrRequest request)
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new
            {
                message = "Ma QR khong hop le."
            });

        var connection = Connection;
        await connection.OpenAsync();

        await using var transaction =
            await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted
            );

        try
        {
            // Khoa theo nguoi dung trong transaction:
            // tranh hai lan quet cung luc tao ban ghi trung.
            await using (var lockCommand = Command(
                connection,
                """
                SELECT pg_advisory_xact_lock(
                    hashtext(@userId)
                )
                """,
                transaction))
            {
                lockCommand.Parameters.AddWithValue(
                    "userId",
                    UserId!
                );

                await lockCommand.ExecuteNonQueryAsync();
            }

            // Atomic update: QR chi duoc dung mot lan.
            await using (var qrCommand = Command(
                connection,
                """
                UPDATE dbo."AttendanceQrCodes"
                SET "UsedAt" = now()
                WHERE "Token" = @token
                  AND "UsedAt" IS NULL
                  AND "ExpiresAt" > now()
                RETURNING "Token"
                """,
                transaction))
            {
                qrCommand.Parameters.AddWithValue(
                    "token",
                    request.Token.Trim()
                );

                var valid = await qrCommand.ExecuteScalarAsync();

                if (valid == null)
                {
                    await transaction.RollbackAsync();

                    return BadRequest(new
                    {
                        message =
                            "Ma QR khong ton tai, da het han " +
                            "hoac da duoc su dung."
                    });
                }
            }

            long? openAttendanceId = null;

            // Tim lan cham vao chua cham ra.
            await using (var findCommand = Command(
                connection,
                """
                SELECT "Id"
                FROM dbo."AttendanceRecords"
                WHERE "UserId" = @userId
                  AND "ClockOut" IS NULL
                ORDER BY "ClockIn" DESC
                LIMIT 1
                """,
                transaction))
            {
                findCommand.Parameters.AddWithValue(
                    "userId",
                    UserId!
                );

                var result =
                    await findCommand.ExecuteScalarAsync();

                if (result != null)
                    openAttendanceId = Convert.ToInt64(result);
            }

            string action;
            long recordId;

            if (openAttendanceId.HasValue)
            {
                // Dang co ca mo: cham ra.
                recordId = openAttendanceId.Value;
                action = "CHECK_OUT";

                await using var updateCommand = Command(
                    connection,
                    """
                    UPDATE dbo."AttendanceRecords"
                    SET "ClockOut" = now()
                    WHERE "Id" = @id
                      AND "UserId" = @userId
                      AND "ClockOut" IS NULL
                    """,
                    transaction
                );

                updateCommand.Parameters.AddWithValue(
                    "id",
                    recordId
                );

                updateCommand.Parameters.AddWithValue(
                    "userId",
                    UserId!
                );

                await updateCommand.ExecuteNonQueryAsync();
            }
            else
            {
                // Chua co ca mo: cham vao.
                action = "CHECK_IN";

                await using var insertCommand = Command(
                    connection,
                    """
                    INSERT INTO dbo."AttendanceRecords"
                    ("UserId", "EmployeeName", "ClockIn", "Method")
                    VALUES (@userId, @name, now(), 'QR')
                    RETURNING "Id"
                    """,
                    transaction
                );

                insertCommand.Parameters.AddWithValue(
                    "userId",
                    UserId!
                );

                insertCommand.Parameters.AddWithValue(
                    "name",
                    UserName
                );

                recordId = Convert.ToInt64(
                    await insertCommand.ExecuteScalarAsync()
                );
            }

            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                action,
                recordId,
                message = action == "CHECK_IN"
                    ? "Cham vao thanh cong."
                    : "Cham ra thanh cong."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    // GET /api/attendance/history
    // Admin xem tat ca, Employee chi xem cua minh.
    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        if (string.IsNullOrWhiteSpace(UserId))
            return Unauthorized();

        var connection = Connection;
        await connection.OpenAsync();

        try
        {
            string query = User.IsInRole("Admin")
                ? """
                  SELECT "Id", "UserId", "EmployeeName",
                         "ClockIn", "ClockOut", "Method"
                  FROM dbo."AttendanceRecords"
                  ORDER BY "ClockIn" DESC
                  LIMIT 200
                  """
                : """
                  SELECT "Id", "UserId", "EmployeeName",
                         "ClockIn", "ClockOut", "Method"
                  FROM dbo."AttendanceRecords"
                  WHERE "UserId" = @userId
                  ORDER BY "ClockIn" DESC
                  LIMIT 200
                  """;

            await using var cmd = Command(connection, query);

            if (!User.IsInRole("Admin"))
            {
                cmd.Parameters.AddWithValue(
                    "userId",
                    UserId!
                );
            }

            var records = new List<object>();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                records.Add(new
                {
                    id = reader.GetInt64(0),
                    userId = reader.GetString(1),
                    employeeName = reader.GetString(2),
                    clockIn = reader.GetDateTime(3),
                    clockOut = reader.IsDBNull(4)
                        ? (DateTime?)null
                        : reader.GetDateTime(4),
                    method = reader.GetString(5)
                });
            }

            return Ok(records);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
