using Microsoft.AspNetCore.Mvc;
using TransferFlow.Api.Contracts.Transfers;
using TransferFlow.Application.Common;
using TransferFlow.Application.Transfers;

namespace TransferFlow.Api.Controllers;

[ApiController]
[Route("transfers")]
public sealed class TransfersController : ControllerBase
{
    private readonly CreateTransferUseCase _createTransferUseCase;
    private readonly GetTransferByIdUseCase _getTransferByIdUseCase;

    public TransfersController(
        CreateTransferUseCase createTransferUseCase,
        GetTransferByIdUseCase getTransferByIdUseCase)
    {
        _createTransferUseCase = createTransferUseCase;
        _getTransferByIdUseCase = getTransferByIdUseCase;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransferResponse>> GetTransferById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var transfer = 
            await _getTransferByIdUseCase.ExecuteAsync(
                id, 
                cancellationToken);

        if (transfer is null)
        {
            return NotFound();
        }

        return Ok(transfer);
    }

    [HttpPost]
    public async Task<ActionResult<TransferResponse>> CreateTransferAsync(
        CreateTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var transfer = await _createTransferUseCase.ExecuteAsync(
                request.SourceWalletId,
                request.DestinationWalletId,
                request.Amount,
                idempotencyKey,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetTransferById),
                new { id = transfer.Id },
                transfer);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
