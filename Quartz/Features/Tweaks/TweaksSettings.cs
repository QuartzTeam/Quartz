using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Tweaks;
public sealed class TweaksSettings : ISettingsFile {
    public bool RemoveAllCheckpoints = true;
    public bool RemoveBallCoreParticles = true;
    public bool DisableTileHitGlow = true;
    public bool RemovePlanetGlow = true;
    public bool DisableAutoPause = true;
    public bool BlockMouseWheelScrollWhilePlaying = true;
    public bool DisableMenuMusic = true;
    public bool MenuBpmEnabled = false;
    public float MenuSlowBpm = 100f;
    public float MenuHighBpm = 200f;
    public JToken Serialize() =>
        new JObject {
            [nameof(RemoveAllCheckpoints)] = RemoveAllCheckpoints,
            [nameof(RemoveBallCoreParticles)] = RemoveBallCoreParticles,
            [nameof(DisableTileHitGlow)] = DisableTileHitGlow,
            [nameof(RemovePlanetGlow)] = RemovePlanetGlow,
            [nameof(DisableAutoPause)] = DisableAutoPause,
            [nameof(BlockMouseWheelScrollWhilePlaying)] = BlockMouseWheelScrollWhilePlaying,
            [nameof(DisableMenuMusic)] = DisableMenuMusic,
            [nameof(MenuBpmEnabled)] = MenuBpmEnabled,
            [nameof(MenuSlowBpm)] = MenuSlowBpm,
            [nameof(MenuHighBpm)] = MenuHighBpm,
        };
    public void Deserialize(JToken token) {
        RemoveAllCheckpoints = IOUtils.Read(token, nameof(RemoveAllCheckpoints), RemoveAllCheckpoints);
        RemoveBallCoreParticles = IOUtils.Read(token, nameof(RemoveBallCoreParticles), RemoveBallCoreParticles);
        DisableTileHitGlow = IOUtils.Read(token, nameof(DisableTileHitGlow), DisableTileHitGlow);
        RemovePlanetGlow = IOUtils.Read(token, nameof(RemovePlanetGlow), RemovePlanetGlow);
        DisableAutoPause = IOUtils.Read(token, nameof(DisableAutoPause), DisableAutoPause);
        BlockMouseWheelScrollWhilePlaying = IOUtils.Read(token, nameof(BlockMouseWheelScrollWhilePlaying), BlockMouseWheelScrollWhilePlaying);
        DisableMenuMusic = IOUtils.Read(token, nameof(DisableMenuMusic), DisableMenuMusic);
        MenuBpmEnabled = IOUtils.Read(token, nameof(MenuBpmEnabled), MenuBpmEnabled);
        MenuSlowBpm = IOUtils.Read(token, nameof(MenuSlowBpm), MenuSlowBpm);
        MenuHighBpm = IOUtils.Read(token, nameof(MenuHighBpm), MenuHighBpm);
    }
}
