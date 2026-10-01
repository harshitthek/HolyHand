namespace HolyHand.Core.Jev;

public interface IJevClient
{
    Task<EvaluateResponse> EvaluateAsync(EvaluateRequest request, CancellationToken cancellationToken = default);
}
