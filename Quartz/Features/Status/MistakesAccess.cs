using Quartz.Compat.Game;
namespace Quartz.Features.Status
{
    internal static class MistakesAccess
    {
        internal static scrMistakesManager Get() => GameApi.MistakesManager;
        internal static float PercentAcc(scrMistakesManager m) => GameApi.PercentAcc(m);
        internal static float PercentXAcc(scrMistakesManager m) => GameApi.PercentXAcc(m);
        internal static int PlayerCount() => GameApi.PlayerCount();
        internal static object Tracker(int playerID) => GameApi.Tracker(playerID);
    }
}
