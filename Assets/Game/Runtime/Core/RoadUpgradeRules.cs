using System;

namespace RoadOfLife
{
    public static class RoadUpgradeRules
    {
        public const int CorrectionAmount = 8;

        public static bool TryGetCorrection(RoadUpgrade upgrade, CardTag tag, out RoadStat stat)
        {
            switch (upgrade)
            {
                case RoadUpgrade.RoadMarkers when tag == CardTag.Visibility:
                    stat = RoadStat.Visibility;
                    return true;
                case RoadUpgrade.WarmingPoint when tag == CardTag.Snow || tag == CardTag.Engine:
                    stat = RoadStat.Engine;
                    return true;
                case RoadUpgrade.PreparedDetour when tag == CardTag.Ice:
                    stat = RoadStat.Tempo;
                    return true;
                case RoadUpgrade.LoadingPost when tag == CardTag.Load:
                    stat = RoadStat.Load;
                    return true;
                default:
                    stat = default;
                    return false;
            }
        }

        public static RoadStat GetTargetStat(RoadUpgrade upgrade) => upgrade switch
        {
            RoadUpgrade.RoadMarkers => RoadStat.Visibility,
            RoadUpgrade.WarmingPoint => RoadStat.Engine,
            RoadUpgrade.PreparedDetour => RoadStat.Tempo,
            RoadUpgrade.LoadingPost => RoadStat.Load,
            _ => throw new ArgumentOutOfRangeException(nameof(upgrade), upgrade, null),
        };
    }
}
