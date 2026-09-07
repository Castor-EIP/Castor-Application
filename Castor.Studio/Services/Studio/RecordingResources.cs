using LibObs;

namespace CastorApplication.Services.Studio
{
    internal sealed record RecordingResources(
        ObsOutput Output,
        ObsEncoder? VideoEncoder,
        ObsEncoder? AudioEncoder);
}
