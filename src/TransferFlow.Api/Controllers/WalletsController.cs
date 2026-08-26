using Microsoft.AspNetCore.Mvc;
using TransferFlow.Application.Wallets;

namespace TransferFlow.Api.Controllers;

[ApiController]
[Route("wallets")]
public sealed class WalletsController : ControllerBase
{
    private readonly CreateWalletUseCase _createWalletUseCase;
    private readonly GetWalletByIdUseCase _getWalletByIdUseCase;

    public WalletsController(
        CreateWalletUseCase createWalletUseCase,
        GetWalletByIdUseCase getWalletByIdUseCase)
    {
        _createWalletUseCase = createWalletUseCase;
        _getWalletByIdUseCase = getWalletByIdUseCase;
    }

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
}