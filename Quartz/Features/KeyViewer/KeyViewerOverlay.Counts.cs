using System.Globalization;
using UnityEngine;

using TMPro;

namespace Quartz.Features.KeyViewer;

// Counter-text helpers: alloc-free integer→TMP writes via a shared char[].
// FormatCount still allocates; the per-frame SetCount/SetPrefixedCount paths
// are the hot ones (called for every box, every frame, while keys spam).
public static partial class KeyViewerOverlay {
    // Counter text: optionally with a thousands separator (v1 count formatting).
    private static string FormatCount(int value) =>
        Conf != null && Conf.CountFormatting
            ? value.ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    // Allocation-free integer -> TMP text for the per-frame hot path (per-key
    // count + per-key KPS, up to 16 boxes, each changing nearly every frame
    // while spamming). FormatCount allocates a string and takes TMP's heavier
    // string-set path; at high KPS that per-frame garbage drives GC hitches the
    // viewer reads as "lagging behind". TMP.SetText(char[], start, length)
    // reuses this shared buffer with no allocation (mirrors the JipperKeyViewer
    // reference's NumBuffer). Main-thread only (Updater.Update), so unsynced.
    private static readonly char[] countBuf = new char[16];
    // Prefixed variant ("KPS  123"): cached caption chars + the digits from
    // countBuf. Grown to fit the longest prefix seen (DM displayText is
    // user-defined, so its length is unbounded).
    private static char[] prefixedCountBuf = new char[32];

    private static void SetCount(TextMeshProUGUI tmp, int value)
        => SetCount(tmp, value, Conf != null && Conf.CountFormatting);

    // DM Note counters pass thousands: false — they always render the plain
    // invariant integer, regardless of the CountFormatting toggle.
    private static void SetCount(TextMeshProUGUI tmp, int value, bool thousands) {
        int pos = WriteCountDigits(value, thousands);
        tmp.SetText(countBuf, pos, countBuf.Length - pos);
    }

    private static void SetPrefixedCount(TextMeshProUGUI tmp, char[] prefix, int value)
        => SetPrefixedCount(tmp, prefix, value, Conf != null && Conf.CountFormatting);

    private static void SetPrefixedCount(TextMeshProUGUI tmp, char[] prefix, int value, bool thousands) {
        int pos = WriteCountDigits(value, thousands);
        int digits = countBuf.Length - pos;
        int len = prefix.Length + digits;
        if(prefixedCountBuf.Length < len) prefixedCountBuf = new char[len * 2];
        Array.Copy(prefix, prefixedCountBuf, prefix.Length);
        Array.Copy(countBuf, pos, prefixedCountBuf, prefix.Length, digits);
        tmp.SetText(prefixedCountBuf, 0, len);
    }

    // Digits (optionally comma-grouped, mirroring FormatCount's "N0" output)
    // written right-aligned into countBuf; returns the start index.
    private static int WriteCountDigits(int value, bool thousands) {
        int pos = countBuf.Length;
        if(value == 0) {
            countBuf[--pos] = '0';
        } else {
            long v = value;
            bool neg = v < 0;
            if(neg) v = -v;
            int seg = 0;
            while(v > 0) {
                if(thousands && seg == 3) { countBuf[--pos] = ','; seg = 0; }
                countBuf[--pos] = (char)('0' + (int)(v % 10));
                v /= 10;
                seg++;
            }
            if(neg) countBuf[--pos] = '-';
        }
        return pos;
    }
}