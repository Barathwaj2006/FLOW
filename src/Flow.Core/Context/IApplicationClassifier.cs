namespace Flow.Core.Context;

/// <summary>
/// Contract for determining the application category of a target application.
/// </summary>
public interface IApplicationClassifier
{
    ApplicationCategory Classify(ForegroundTargetInfo targetInfo);
}
