using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
namespace Quartz.Features.KeyLimiter;
public sealed class KeyLimiterProfile {
    public string Name = "";
    public int[] Keys = [];
}
public sealed class KeyLimiterSettings : ISettingsFile {
    public bool Enabled = true;
    public bool BlockInputsWhileMenuOpen = true;
    public List<KeyLimiterProfile> Profiles = [
        new KeyLimiterProfile {
            Name = "Profile 1",
            Keys = [
                113, 51, 52, 116, 111, 45, 61, 92,
                32, 98, 104, 46, 97, 304, 273, 13,
            ],
        },
    ];
    public int ActiveProfile = 0;
    public int[] AllowedKeys {
        get => ActiveProfileOrDefault().Keys;
        set => ActiveProfileOrDefault().Keys = value ?? [];
    }
    public KeyLimiterProfile ActiveProfileOrDefault() {
        if(Profiles == null || Profiles.Count == 0) Profiles = [new KeyLimiterProfile { Name = "Profile 1", Keys = [] }];
        if(ActiveProfile < 0 || ActiveProfile >= Profiles.Count) ActiveProfile = 0;
        return Profiles[ActiveProfile];
    }
    public JToken Serialize() {
        JArray profiles = [];
        foreach(KeyLimiterProfile p in Profiles) {
            profiles.Add(new JObject {
                ["Name"] = p.Name ?? "",
                ["Keys"] = new JArray(p.Keys ?? []),
            });
        }
        return new JObject {
            [nameof(Enabled)] = Enabled,
            [nameof(BlockInputsWhileMenuOpen)] = BlockInputsWhileMenuOpen,
            [nameof(Profiles)] = profiles,
            [nameof(ActiveProfile)] = ActiveProfile,
        };
    }
    public void Deserialize(JToken token) {
        Enabled = IOUtils.Read(token, nameof(Enabled), Enabled);
        BlockInputsWhileMenuOpen = IOUtils.Read(token, nameof(BlockInputsWhileMenuOpen), BlockInputsWhileMenuOpen);
        if(token?[nameof(Profiles)] is JArray profArr) {
            List<KeyLimiterProfile> list = [];
            int index = 1;
            foreach(JToken pt in profArr) {
                list.Add(new KeyLimiterProfile {
                    Name = IOUtils.Read(pt, "Name", "Profile " + index),
                    Keys = ReadKeys(pt?["Keys"]),
                });
                index++;
            }
            if(list.Count > 0) Profiles = list;
            ActiveProfile = IOUtils.Read(token, nameof(ActiveProfile), 0);
        } else if(token?["AllowedKeys"] is JArray legacy) {
            Profiles = [new KeyLimiterProfile { Name = "Profile 1", Keys = ReadKeys(legacy) }];
            ActiveProfile = 0;
        }
        if(Profiles == null || Profiles.Count == 0) Profiles = [new KeyLimiterProfile { Name = "Profile 1", Keys = [] }];
        if(ActiveProfile < 0 || ActiveProfile >= Profiles.Count) ActiveProfile = 0;
    }
    private static int[] ReadKeys(JToken token) {
        if(token is not JArray arr) return [];
        List<int> keys = [];
        foreach(JToken t in arr)
            try { keys.Add((int)t); } catch { }
        return [.. keys];
    }
}
