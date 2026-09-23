using Microsoft.AspNetCore.Mvc;
using TransferFlow.Application.Wallets;

namespace TransferFlow.Api.Controllers;

[ApiController]
[Route("wallets")]
public sealed class WalletsController(
    CreateWalletUseCase createWalletUseCase,
    GetWalletByIdUseCase getWalletByIdUseCase,
    GetWalletActivitiesUseCase getWalletActivitiesUseCase) : ControllerBase
{
    private readonly CreateWalletUseCase _createWalletUseCase = createWalletUseCase;
    private readonly GetWalletByIdUseCase _getWalletByIdUseCase = getWalletByIdUseCase;
    private readonly GetWalletActivitiesUseCase _getWalletActivitiesUseCase = getWalletActivitiesUseCase;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WalletResponse>> GetWalletById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var wallet = await _getWalletByIdUseCase.ExecuteAsync(id, cancellationToken);
        if (wallet is null)
        {
            return NotFound();
        }
        return Ok(wallet);
    }

    [HttpPost]
    public async Task<ActionResult<WalletResponse>> CreateWalletAsync(
        CancellationToken cancellationToken)
    {
        var wallet = await _createWalletUseCase.ExecuteAsync(cancellationToken);
        return CreatedAtAction(
            nameof(GetWalletById), 
            new { id = wallet.Id }, 
            wallet);
    }

    [HttpGet("{id:guid}/activities")]
    public async Task<ActionResult<IReadOnlyList<WalletActivityResponse>>>GetWalletActivitiesAsync(
        Guid id,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
        {
            return BadRequest(
                "Limit must be between 1 and 100.");
        }

        var activities =
            await _getWalletActivitiesUseCase.ExecuteAsync(
                id,
                limit,
                cancellationToken);

        if (activities is null)
        {
            return NotFound();
        }

        return Ok(activities);
    }
}
