using System.Collections.Generic;
using UnityEngine;

namespace Quartz.Features.KeyViewer;

// Slot geometry per style, shared by the overlay and the settings-page
// preview. v1 SimplePresets.BuildKey10/12/16/20.
public static partial class KeyViewerOverlay {
    private static float ColX(int column) => (KeyW + KeyGap) * column;

    private static float SpanW(int columns) => KeyW * columns + KeyGap * (columns - 1);

    internal readonly struct KeySlot(int slot, float x, float y, float w, float h) {
        public readonly int Slot = slot;
        public readonly float X = x, Y = y, W = w, H = h;
    }

    internal readonly struct StatSlot(bool total, float x, float y, float w, float h) {
        public readonly bool Total = total;
        public readonly float X = x, Y = y, W = w, H = h;
    }

    internal static void BuildLayout(int style, List<KeySlot> keys, List<StatSlot> stats) {
        // Front row: always the first 8 keys.
        for(int i = 0; i < 8; i++) keys.Add(new KeySlot(i, ColX(i), 0f, KeyW, KeyH));

        // KPS/Total sit on the outer edges with the back-row keys between them.
        // (The Together/Apart setting no longer moves these boxes — it controls
        // the caption/value arrangement inside each stat box; see AddStat.)
        switch(style) {
            case 0:
                keys.Add(new KeySlot(8, ColX(2), RowGap, SpanW(2), KeyH));
                keys.Add(new KeySlot(9, ColX(4), RowGap, SpanW(2), KeyH));
                stats.Add(new StatSlot(false, ColX(0), RowGap, SpanW(2), KeyH));
                stats.Add(new StatSlot(true, ColX(6), RowGap, SpanW(2), KeyH));
                break;
            case 1:
                keys.Add(new KeySlot(9, ColX(2), RowGap, KeyW, KeyH));
                keys.Add(new KeySlot(8, ColX(3), RowGap, KeyW, KeyH));
                keys.Add(new KeySlot(10, ColX(4), RowGap, KeyW, KeyH));
                keys.Add(new KeySlot(11, ColX(5), RowGap, KeyW, KeyH));
                stats.Add(new StatSlot(false, ColX(0), RowGap, SpanW(2), KeyH));
                stats.Add(new StatSlot(true, ColX(6), RowGap, SpanW(2), KeyH));
                break;
            case 2:
                for(int i = 0; i < 8; i++) keys.Add(new KeySlot(BackSeq16[i], ColX(i), RowGap, KeyW, KeyH));
                stats.Add(new StatSlot(false, ColX(0), RowGap * 2f, SpanW(4), CompactStatH));
                stats.Add(new StatSlot(true, ColX(4), RowGap * 2f, SpanW(4), CompactStatH));
                break;
            case 3:
                for(int i = 0; i < 8; i++) keys.Add(new KeySlot(BackSeq16[i], ColX(i), RowGap, KeyW, KeyH));
                keys.Add(new KeySlot(17, ColX(2), RowGap * 2f, KeyW, KeyH));
                keys.Add(new KeySlot(16, ColX(3), RowGap * 2f, KeyW, KeyH));
                keys.Add(new KeySlot(18, ColX(4), RowGap * 2f, KeyW, KeyH));
                keys.Add(new KeySlot(19, ColX(5), RowGap * 2f, KeyW, KeyH));
                stats.Add(new StatSlot(false, ColX(0), RowGap * 2f, SpanW(2), KeyH));
                stats.Add(new StatSlot(true, ColX(6), RowGap * 2f, SpanW(2), KeyH));
                break;
            case 4:
                // 8 keys: the front row only, with the stat boxes on the row
                // below (no back row).
                stats.Add(new StatSlot(false, ColX(0), RowGap, SpanW(4), CompactStatH));
                stats.Add(new StatSlot(true, ColX(4), RowGap, SpanW(4), CompactStatH));
                break;
            case 5:
                // 14 keys: front row + a 6-key back row centred on columns 1-6,
                // stats on a third row like the 20-key style.
                for(int i = 0; i < 6; i++) keys.Add(new KeySlot(8 + i, ColX(1 + i), RowGap, KeyW, KeyH));
                stats.Add(new StatSlot(false, ColX(0), RowGap * 2f, SpanW(2), KeyH));
                stats.Add(new StatSlot(true, ColX(6), RowGap * 2f, SpanW(2), KeyH));
                break;
        }
    }

    internal static Vector2 GridSize(int style) => new(SpanW(8), style switch {
        2 => RowGap * 2f + CompactStatH,
        3 => RowGap * 2f + KeyH,
        4 => RowGap + CompactStatH,
        5 => RowGap * 2f + KeyH,
        _ => RowGap + KeyH,
    });

    // Foot-key layout in the foot element's OWN local space (top-left origin),
    // independent of the main grid. footCount 0 = none. Fills footSlots with
    // KeySlots whose Slot is FootSlotBase + foot index, and returns the foot
    // block's size. Up to 8 keys per row, centred; a second row centres below.
    internal static Vector2 BuildFootLayout(int footCount, List<KeySlot> footSlots) {
        if(footCount <= 0) return Vector2.zero;

        int row1 = Mathf.Min(footCount, 8);
        int row2 = Mathf.Max(0, footCount - 8);

        static float RowW(int n) => n <= 0 ? 0f : n * FootKeyW + (n - 1) * FootKeyGap;
        float blockW = Mathf.Max(RowW(row1), RowW(row2));

        void Row(int startIndex, int count, float y) {
            float startX = (blockW - RowW(count)) * 0.5f;
            for(int i = 0; i < count; i++) {
                int footIndex = startIndex + i;
                footSlots.Add(new KeySlot(
                    KeyViewerSettings.FootSlotBase + footIndex,
                    startX + i * (FootKeyW + FootKeyGap), y, FootKeyW, FootKeyH));
            }
        }

        Row(0, row1, 0f);
        if(row2 > 0) Row(8, row2, FootRowPitch);

        float blockH = row2 > 0 ? FootRowPitch + FootKeyH : FootKeyH;
        return new Vector2(blockW, blockH);
    }

    // Combined size used ONLY by the settings-page preview, which stacks the
    // foot block under the main grid for rebinding (the live overlay keeps them
    // as two separate elements). Returns the bounding size; the page offsets the
    // foot rows down by main height + gap.
    internal static Vector2 GridSizeWithFoot(int style, int footCount) {
        Vector2 main = GridSize(style);
        if(footCount <= 0) return main;
        List<KeySlot> footSlots = [];
        Vector2 foot = BuildFootLayout(footCount, footSlots);
        return new Vector2(Mathf.Max(main.x, foot.x), main.y + FootGapAbove + foot.y);
    }

    // Builds the foot-key element (its own root, sized + reorganize handle).
    private static void BuildFoot() {
        if(footRoot == null) return;

        int footCount = Conf.FootKeyCount();
        if(!Conf.IsSimpleMode || footCount <= 0) {
            footRoot.sizeDelta = Vector2.zero;
            return;
        }

        List<KeySlot> footSlots = [];
        Vector2 footSize = BuildFootLayout(footCount, footSlots);
        footRoot.sizeDelta = footSize;

        foreach(KeySlot slot in footSlots) AddFootKey(slot.Slot - KeyViewerSettings.FootSlotBase, slot.X, slot.Y, slot.W, slot.H);

        AddFootReorganizeHandle();
    }
}