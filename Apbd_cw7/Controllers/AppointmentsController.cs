using Apbd_cw7.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Apbd_cw7.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public AppointmentsController(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] string? status,
        [FromQuery] string? patientLastName)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var appointments = new List<AppointmentListDto>();

        var sql = @"
            SELECT
                a.IdAppointment,
                a.AppointmentDate,
                a.Status,
                a.Reason,
                p.FirstName + N' ' + p.LastName AS PatientFullName,
                p.Email AS PatientEmail
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
              AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
            ORDER BY a.AppointmentDate";

        await using var connection = new SqlConnection(connectionString);
        await using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("@PatientLastName", (object?)patientLastName ?? DBNull.Value);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.IsDBNull(reader.GetOrdinal("Reason"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.IsDBNull(reader.GetOrdinal("PatientEmail"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("PatientEmail"))
            });
        }

        return Ok(appointments);
    }

    [HttpGet("{idAppointment:int}")]
    public async Task<IActionResult> GetAppointment(int idAppointment)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        var sql = @"
    SELECT
        a.IdAppointment,
        a.AppointmentDate,
        a.Status,
        a.Reason,
        a.InternalNotes,
        a.CreatedAt,
        a.IdPatient,
        p.FirstName + N' ' + p.LastName AS PatientFullName,
        p.Email AS PatientEmail,
        p.PhoneNumber AS PatientPhoneNumber,
        d.FirstName + N' ' + d.LastName AS DoctorFullName,
        d.IdSpecialization AS DoctorIdSpecialization
    FROM dbo.Appointments a
    JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
    JOIN dbo.Doctors d  ON d.IdDoctor  = a.IdDoctor
    WHERE a.IdAppointment = @IdAppointment";
        await using var connection = new SqlConnection(connectionString);
        await using var command = new SqlCommand(sql, connection);

        command.Parameters.AddWithValue("@IdAppointment", idAppointment);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return NotFound($"Wizyta o id={idAppointment} nie istnieje.");
        var dto = new AppointmentDetailsDto
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.IsDBNull(reader.GetOrdinal("Reason"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes"))
                ? null
                : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

            IdPatient = reader.GetInt32(reader.GetOrdinal("IdPatient")),
            PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
            PatientEmail = reader.IsDBNull(reader.GetOrdinal("PatientEmail"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("PatientEmail")),
            PatientPhoneNumber = reader.IsDBNull(reader.GetOrdinal("PatientPhoneNumber"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),

            DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
            DoctorIdSpecialization = reader.GetInt32(reader.GetOrdinal("DoctorIdSpecialization"))
        };
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequestDto dto)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.AppointmentDate < DateTime.Now)
            return BadRequest("Data wizyty nie może być w przeszłości.");

        await using var checkPatient =
            new SqlCommand("Select 1 from dbo.Patients where IdPatient = @IdPatient", connection);
        checkPatient.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        if (await checkPatient.ExecuteScalarAsync() is null)
            return NotFound($"Pacjent o id={dto.IdPatient} nie istnieje.");

        await using var checkDoctor = new SqlCommand(
            "SELECT 1 FROM dbo.Doctors WHERE IdDoctor = @IdDoctor", connection);
        checkDoctor.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        if (await checkDoctor.ExecuteScalarAsync() is null)
            return NotFound($"Doctor {dto.IdDoctor} nie istnieje.");

        await using var checkConflict = new SqlCommand(@"
SELECT 1 FROM dbo.Appointments
Where IdDoctor = @IdDoctor and AppointmentDate = @AppointmentDate", connection);
        checkConflict.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        checkConflict.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        if (await checkConflict.ExecuteScalarAsync() is not null)
            return Conflict($"Lekarz ma już wizytę w terminie{dto.AppointmentDate}.");




        await using var command = new SqlCommand(@"
            Insert into dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Reason, Status)
            OUTPUT INSERTED.IdAppointment
            VALUES (@IdPatient, @IdDoctor, @AppointmentDate, @Reason, @Status)",connection);



        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@Reason", dto.Reason);
        command.Parameters.AddWithValue("@Status", "Scheduled");
        
        var newId = (int)(await command.ExecuteScalarAsync())!;

        return CreatedAtAction(nameof(GetAppointment), new { idAppointment = newId }, new { IdAppointment = newId });
    }

    [HttpPut("{idAppointment:int}")]
    public async Task<IActionResult> UpdateAppointment(int idAppointment, [FromBody] UpdateAppointmentRequestDto dto)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (dto.AppointmentDate < DateTime.Now)
            return BadRequest("Data wizyty nie może być w przeszłości.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var checkAppointment = new SqlCommand(
            "Select 1 from dbo.Appointments where IdAppointment = @IdAppointment", connection);
        checkAppointment.Parameters.AddWithValue("@IdAppointment", idAppointment);
        if (await checkAppointment.ExecuteScalarAsync() is null)
            return NotFound($"Wizyta o id={idAppointment} nie istnieje.");

        await using var checkPatient = new SqlCommand(
            "Select 1 from dbo.Patients where IdPatient = @IdPatient", connection);
        checkPatient.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        if (await checkPatient.ExecuteScalarAsync() is null)
            return NotFound($"Pacjent o id={dto.IdPatient} nie istnieje.");

        await using var checkDoctor = new SqlCommand(
            "SELECT 1 FROM dbo.Doctors WHERE IdDoctor = @IdDoctor", connection);
        checkDoctor.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        if (await checkDoctor.ExecuteScalarAsync() is null)
            return NotFound($"Lekarz o id={dto.IdDoctor} nie istnieje.");

        await using var checkConflict = new SqlCommand(@"
        SELECT 1 FROM dbo.Appointments
        WHERE IdDoctor = @IdDoctor
          AND AppointmentDate = @AppointmentDate
          AND IdAppointment != @IdAppointment", connection);
        checkConflict.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        checkConflict.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        checkConflict.Parameters.AddWithValue("@IdAppointment", idAppointment);
        if (await checkConflict.ExecuteScalarAsync() is not null)
            return Conflict($"Lekarz ma już wizytę w terminie {dto.AppointmentDate}.");

        await using var command = new SqlCommand(@"
        UPDATE dbo.Appointments
        SET IdPatient       = @IdPatient,
            IdDoctor        = @IdDoctor,
            AppointmentDate = @AppointmentDate,
            Status          = @Status,
            Reason          = @Reason,
            InternalNotes   = @InternalNotes
        WHERE IdAppointment = @IdAppointment", connection);

        command.Parameters.AddWithValue("@IdAppointment", idAppointment);
        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@Status", dto.Status);
        command.Parameters.AddWithValue("@Reason", dto.Reason);
        command.Parameters.AddWithValue("@InternalNotes", (object?)dto.InternalNotes ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();

        return NoContent();
    }

    [HttpDelete("{idAppointment:int}")]
    public async Task<IActionResult> DeleteAppointment(int idAppointment)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        // Sprawdź status przed usunięciem
        await using var checkStatus = new SqlCommand(
            "SELECT Status FROM dbo.Appointments WHERE IdAppointment = @IdAppointment", connection);
        checkStatus.Parameters.AddWithValue("@IdAppointment", idAppointment);
        var status = await checkStatus.ExecuteScalarAsync() as string;

        if (status is null)
            return NotFound($"Wizyta o id={idAppointment} nie istnieje.");

        if (status == "Completed")
            return Conflict("Nie można usunąć wizyty o statusie Completed.");
        
        await using var checkAppointment = new SqlCommand(
            "SELECT 1 FROM dbo.Appointments WHERE IdAppointment = @IdAppointment", connection);
        checkAppointment.Parameters.AddWithValue("@IdAppointment", idAppointment);
        if (await checkAppointment.ExecuteScalarAsync() is null)
            return NotFound($"Wizyta o id={idAppointment} nie istnieje.");
        
        await using var command = new SqlCommand(
            "DELETE FROM dbo.Appointments WHERE IdAppointment = @IdAppointment", connection);
        command.Parameters.AddWithValue("@IdAppointment", idAppointment);
        await command.ExecuteNonQueryAsync();

        return NoContent();
    }
}