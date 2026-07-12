List<(string Name, Action Run)> tests = [
    ("SemVer parses and orders channels", SemVerTests.TestSemVer),
    ("SemVer formats and parses channels", SemVerTests.TestSemVerFormatAndChannels),
    ("AtomicFile replaces without temp debris", AtomicFileTests.TestAtomicFile),
    ("Imported mod profile names are sanitized and uniquified", ProfileNamesTests.TestImportedModProfileNames),
    ("Localization keys stay in parity", LocalizationParityTests.TestLocalizationParity),
    ("KeyViewer CSS parses the DM Note contract", KeyViewerCssTests.TestKeyViewerCss),
    ("KeyViewer CSS parses the extended web effects", KeyViewerCssTests.TestKeyViewerCssExtended),
    ("KeyViewer persistence avoids gameplay saves and strips unused preset data", KeyViewerCssTests.TestKeyViewerPersistence),
    ("TUF inputs and download URLs are validated", TufSecurityTests.TestInputAndNetworkPolicy),
    ("TUF archives reject unsafe entries and select charts", TufSecurityTests.TestArchiveSafetyAndSelection),
    ("TUF difficulty filters clamp, select, and reset", TufFilterTests.TestDifficultyFilterContract),
    ("TUF API emits named PGU and special filters", TufFilterTests.TestApiDifficultyQuery),
    ("TUF quantum range selects, clamps, and clears", TufFilterTests.TestQuantumRange),
    ("TUF browser preferences serialize and normalize", TufFilterTests.TestPersistedPreferences),
    ("TUF pack list parses string ids and counts", TufPackParseTests.TestPackListParsing),
    ("TUF pack tree flattens, dedups, and credits charters", TufPackParseTests.TestPackTreeParsing),
];
int failed = 0;
foreach((string name, Action run) in tests) {
    try {
        run();
        Console.WriteLine("PASS " + name);
    } catch(Exception e) {
        failed++;
        Console.Error.WriteLine("FAIL " + name + ": " + e.Message);
    }
}
return failed == 0 ? 0 : 1;
