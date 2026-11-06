# Eight-patch 368-case recovery matrix retest

All368 Load operations recovered the exact valid backup. 352 controlled InvalidDataException rejections;16 safe JsonException deserialization rejections. The original16 ArgumentNullException escapes are eliminated. No primary accepted and no uncontrolled Load exception.

Every primary and backup retained its SHA256. Fresh fixtures have newly generated IDs, so all368 raw initial hash pairs differ from the original run. Comparing all736 primary/backup texts after normalizing 32-character generated IDs finds zero other differences. Harness Program.cs is byte-identical to original Program.matrix.cs.txt. All112 copied compilation inputs remained unchanged during execution; compared with the old copied source, only FamilyValidation.cs and FamilySocial.cs differ.

Source: e8d96a78f49182eb17e269e2b62a738c7a9bb7ea plus eight applied patches, not a clean commit. Machine Shiki-5060Ti-PC, SDK10.0.402, Release. Product source was copied from alice-combined-eight-20261106, never edited. Parent explicitly permitted running after aggregate checks finished.

Preserved original matrix evidence in alice-save-reference-matrix-20261106. This new directory holds Program.cs, source-hashes.json, run.log, summary.json and evidence/results.json with all cases, errors and file hashes. Full synthetic fixtures remain local. No personal saves, UI testing, commits or pushes.

The unchanged harness field escaped=16 includes safe JsonException; it does not mean16 Load failures. Use summary.json.unhandled_load=0 and recovered_exact=368.
