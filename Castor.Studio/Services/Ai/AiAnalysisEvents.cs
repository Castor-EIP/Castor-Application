using Castor.IA.Proto;

namespace CastorApplication.Services.Ai;

internal sealed record AiSceneSwitchEvent(string SceneId, float Confidence);
internal sealed record AiSessionStatusEvent(SessionState State, string Message);
internal sealed record AiServerErrorEvent(string ErrorCode, string ErrorMessage, bool IsFatal);
