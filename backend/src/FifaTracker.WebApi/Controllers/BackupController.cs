using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using System.IO;

namespace FifaTracker.WebApi.Controllers;

[ApiController]
[Route("api/backup")]
public class BackupController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _backupDirectory;

    public BackupController(IConfiguration configuration)
    {
        _configuration = configuration;
        // Store backups next to the database - for PostgreSQL, this will be where the database is accessible
        // In a production setup, you'd want to configure this properly
        _backupDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backups");

        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
        }
    }

    // GET: api/backup/list
    [HttpGet("list")]
    public IActionResult ListBackups()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "*.backup")
                .Select(file => new
                {
                    fileName = Path.GetFileName(file),
                    createdDate = System.IO.File.GetCreationTime(file),
                    size = new FileInfo(file).Length
                })
                .OrderByDescending(b => b.createdDate)
                .ToList();

            return Ok(new
            {
                backups = backupFiles,
                backupDirectory = _backupDirectory
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to list backups", error = ex.Message });
        }
    }

    // POST: api/backup/create
    [HttpPost("create")]
    public async Task<IActionResult> CreateBackup()
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFileName = $"fifa_tracker_backup_{timestamp}.backup";
            var backupPath = Path.Combine(_backupDirectory, backupFileName);

            // Get PostgreSQL connection details
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var connStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

            var psi = new ProcessStartInfo
            {
                FileName = "pg_dump",
                Arguments = $"--host {connStringBuilder.Host} --port {connStringBuilder.Port} --username {connStringBuilder.Username} --dbname {connStringBuilder.Database} --no-password --format custom --file \"{backupPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set password environment variable for pg_dump
            psi.EnvironmentVariables["PGPASSWORD"] = connStringBuilder.Password;

            using var process = Process.Start(psi);
            if (process == null)
            {
                return StatusCode(500, new { message = "Failed to start backup process" });
            }

            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                // Clean up failed backup file
                if (System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Delete(backupPath);
                }
                return StatusCode(500, new { message = "Backup failed", error = errorOutput });
            }

            return Ok(new
            {
                message = "Backup created successfully",
                backupFile = backupFileName,
                path = backupPath
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Backup creation failed", error = ex.Message });
        }
    }

    // POST: api/backup/restore/{fileName}
    [HttpPost("restore/{fileName}")]
    public async Task<IActionResult> RestoreBackup(string fileName)
    {
        try
        {
            var backupPath = Path.Combine(_backupDirectory, fileName);

            if (!System.IO.File.Exists(backupPath))
            {
                return NotFound(new { message = "Backup file not found" });
            }

            // Get PostgreSQL connection details
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var connStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);

            // First, backup the current state before restore
            var preRestoreBackupName = $"auto_backup_before_restore_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.backup";
            var preRestorePath = Path.Combine(_backupDirectory, preRestoreBackupName);

            // Create safety backup
            var safetyBackupPsi = new ProcessStartInfo
            {
                FileName = "pg_dump",
                Arguments = $"--host {connStringBuilder.Host} --port {connStringBuilder.Port} --username {connStringBuilder.Username} --dbname {connStringBuilder.Database} --no-password --format custom --file \"{preRestorePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            safetyBackupPsi.EnvironmentVariables["PGPASSWORD"] = connStringBuilder.Password;

            using (var safetyProcess = Process.Start(safetyBackupPsi))
            {
                if (safetyProcess != null)
                {
                    await safetyProcess.WaitForExitAsync();
                }
            }

            // Now restore from the selected backup
            // Note: This will overwrite the current database completely
            var psi = new ProcessStartInfo
            {
                FileName = "pg_restore",
                Arguments = $"--host {connStringBuilder.Host} --port {connStringBuilder.Port} --username {connStringBuilder.Username} --dbname {connStringBuilder.Database} --no-password --clean --if-exists --create \"{backupPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["PGPASSWORD"] = connStringBuilder.Password;

            using var process = Process.Start(psi);
            if (process == null)
            {
                return StatusCode(500, new { message = "Failed to start restore process" });
            }

            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return StatusCode(500, new {
                    message = "Restore failed",
                    error = errorOutput,
                    safetyBackupCreated = System.IO.File.Exists(preRestorePath) ? preRestoreBackupName : null
                });
            }

            return Ok(new
            {
                message = "Database restored successfully",
                restoredFrom = fileName,
                safetyBackupCreated = preRestoreBackupName
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Restore failed", error = ex.Message });
        }
    }
}
