using MediatR;
using MentorshipPlatform.Application.Common.Attributes;
using MentorshipPlatform.Application.Common.Interfaces;

namespace MentorshipPlatform.Application.Common.Behaviours;

public class FeatureFlagCheckBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureFlagCheckBehaviour(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attributes = typeof(TRequest)
            .GetCustomAttributes(typeof(RequiresFeatureAttribute), false)
            .Cast<RequiresFeatureAttribute>()
            .ToList();

        if (attributes.Count == 0)
            return await next();

        foreach (var attr in attributes)
        {
            var isEnabled = await _featureFlagService.IsEnabledAsync(attr.FeatureFlagKey, cancellationToken);
            if (!isEnabled)
            {
                throw new FeatureDisabledException(attr.FeatureFlagKey);
            }
        }

        return await next();
    }
}

public class FeatureDisabledException : Exception
{
    public string FeatureFlagKey { get; }

    public FeatureDisabledException(string featureFlagKey)
        : base($"Bu ozellik su anda devre disi: {featureFlagKey}")
    {
        FeatureFlagKey = featureFlagKey;
    }
}
