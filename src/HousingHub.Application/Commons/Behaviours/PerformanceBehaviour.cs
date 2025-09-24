using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HousingHub.Application.Commons.Behaviours;

public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    private readonly Stopwatch _timer;
    private readonly ILogger<TRequest> _logger;
    private readonly int _maxRequestTime = 50000;

    public PerformanceBehaviour(ILogger<TRequest> logger)
    {
        _timer = new Stopwatch() ?? throw new ArgumentNullException(nameof(Stopwatch));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _timer.Start();
        var response = await next();
        _timer.Stop();

        var elapsedMilliseconds = _timer.ElapsedMilliseconds;
        if (elapsedMilliseconds > _maxRequestTime)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogWarning("Service Long Running: {name} ({elapsedMilliseconds} milliseconds) {@request}",
                requestName,
                elapsedMilliseconds,
                JsonSerializer.Serialize(request));
        }
        return response;
    }
}