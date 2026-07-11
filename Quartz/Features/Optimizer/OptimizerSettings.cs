using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.Optimizer;
public sealed class OptimizerSettings : ISettingsFile {
    public bool SmoothGC = true;
    public bool LeakGuard = true;
    public bool CollectOnLevelLoad = true;
    public bool BoostProcessPriority = true;
    public bool RunInBackground = true;
    public bool LossyTextureCompression = false;
    public bool FastBloom = false;
    public bool SkipNoOpScreenFilters = true;
    public bool LightTextShadows = true;
    public float ShadowUnderlayOffsetScale = 6f;
    public JToken Serialize() => new JObject {
        [nameof(SmoothGC)] = SmoothGC,
        [nameof(LeakGuard)] = LeakGuard,
        [nameof(CollectOnLevelLoad)] = CollectOnLevelLoad,
        [nameof(BoostProcessPriority)] = BoostProcessPriority,
        [nameof(RunInBackground)] = RunInBackground,
        [nameof(LossyTextureCompression)] = LossyTextureCompression,
        [nameof(FastBloom)] = FastBloom,
        [nameof(SkipNoOpScreenFilters)] = SkipNoOpScreenFilters,
        [nameof(LightTextShadows)] = LightTextShadows,
        [nameof(ShadowUnderlayOffsetScale)] = ShadowUnderlayOffsetScale,
    };
    public void Deserialize(JToken token) {
        SmoothGC = IOUtils.Read(token, nameof(SmoothGC), SmoothGC);
        LeakGuard = IOUtils.Read(token, nameof(LeakGuard), LeakGuard);
        CollectOnLevelLoad = IOUtils.Read(token, nameof(CollectOnLevelLoad), CollectOnLevelLoad);
        BoostProcessPriority = IOUtils.Read(token, nameof(BoostProcessPriority), BoostProcessPriority);
        RunInBackground = IOUtils.Read(token, nameof(RunInBackground), RunInBackground);
        LossyTextureCompression = IOUtils.Read(token, nameof(LossyTextureCompression), LossyTextureCompression);
        FastBloom = IOUtils.Read(token, nameof(FastBloom), FastBloom);
        SkipNoOpScreenFilters = IOUtils.Read(token, nameof(SkipNoOpScreenFilters), SkipNoOpScreenFilters);
        LightTextShadows = IOUtils.Read(token, nameof(LightTextShadows), LightTextShadows);
        ShadowUnderlayOffsetScale = IOUtils.Read(token, nameof(ShadowUnderlayOffsetScale), ShadowUnderlayOffsetScale);
    }
}
