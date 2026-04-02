using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Abstractions;
using POS.Application.Models;

namespace POS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/backup")]
public sealed class BackupController : ControllerBase
{
    private readonly IBackupRestoreService _backupRestoreService;

    public BackupController(IBackupRestoreService backupRestoreService)
    {
        _backupRestoreService = backupRestoreService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<BackupResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<BackupResult>>> ListBackups(CancellationToken cancellationToken)
    {
        var backups = await _backupRestoreService.ListBackupsAsync(cancellationToken);
        return Ok(backups);
    }

    [HttpPost("manual")]
    [ProducesResponseType(typeof(BackupResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupResult>> CreateManualBackup(CancellationToken cancellationToken)
    {
        var backup = await _backupRestoreService.CreateBackupAsync(isAutomatic: false, cancellationToken);
        return Ok(backup);
    }

    [HttpPost("restore")]
    [ProducesResponseType(typeof(RestoreResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<RestoreResult>> Restore([FromBody] RestoreRequest request, CancellationToken cancellationToken)
    {
        var result = await _backupRestoreService.RestoreBackupAsync(request, cancellationToken);
        return Ok(result);
    }
}
