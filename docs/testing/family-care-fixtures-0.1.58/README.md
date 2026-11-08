# v0.1.58: Family.CareContinuity fixture adaptation

Status: partial, not a full test pass.

Baseline commit: 3da3bf948052e875364a4ed6dbb207a9f3b3843c.

Scope: two obsolete adult-age assignments in Family.CareContinuity now use GrowthRules.Initialize. The game project version changes from 0.1.55 to 0.1.58. No production gameplay rule changed. All six original check bodies, real harm and interruption, inventory, movement, transport, intake, timing, identity and relationship assertions remain unchanged. There are 37 actual Require calls before and after; counting the function declaration would incorrectly give 38.

Focused result: 0/6 before, 4/6 after. The passing cases cover recent harmful-parent warmth without social credit, safe-parent care, continued harmful-care restrictions beyond 30 seconds, and paused/reloaded transport. The failing delivery group completed its child meal and child water cases, then failed to establish actual harm for the adult meal case; adult water was not reached. Automatic wild grass reappears after the fixture clears food, and the hungry actor is later observed seeking ordinary food. These observations are consistent with the resumed food-seeking path, but do not fully establish why the attack was cancelled: neither the cancellation instant nor the relevant ReachableOrdinaryHungerFood return value was captured. The sustained-care case reaches the start of night at world second 350 and is correctly refused while the caregiver is resting. The fixture had not established continued caregiver availability across the night transition; the refusal is consistent with the existing rule, not evidence that the rule is defective. Neither failure was hidden or bypassed.

Related suites are unchanged before and after: Family.Growth 3/5, Courtyard.SurvivalGrowth 14/14, Family.ColdCare 3/9, Family.Aftermath 13/17. Their existing failures remain open. Three separate diagnostic age/flag/version/save checks pass, including rejection of the inconsistent 2000-second adult fixture; these are not added to the original six.

The final Debug game was force rebuilt successfully: 0 errors and 4 nullable warnings. The assembly version is 0.1.58. Execution was serial, single-core and BelowNormal, using fresh isolated data/cache directories. The bounded run used no Godot or visible game.

Not covered: the full suite, visual/audio/input/playability acceptance, long-duration stability, historical personal saves, or the unexecuted adult-water branch. This change does not close broader gameplay acceptance.
Changed-file SHA256 (final):
- src/Yukkuri.Game/Yukkuri.Game.csproj: 6c43d0abd78cf5e8d62c65adef76e94e9b3a033dd44aee8eeb90d0527fcdb9c8
- checks/Family.CareContinuity/Program.cs: fc4392d2ff7bf61b7e6412ee09d1c95cf5ee01a999b71035540ebc2f9e8c0627
