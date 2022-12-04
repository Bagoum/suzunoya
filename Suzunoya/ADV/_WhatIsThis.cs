/*

Suzunoya contains support for visual novel (VN) management.
The files in this namespace (Suzunoya.ADV) extends this to adventure-game-style visual novel (ADV) management.
While VN execution is composed more-or-less solely of the player advancing through text, 
 ADV execution can involve intermediary phases such as the player interacting with the environment,
 and the ability to show text is preserved on the side.
As ADV games do not have linear execution structures, state management (eg. deciding what characters show up at 
 what times and how the player can interact with them) is much more complex. This type of state management is handled
 via "assertions" and "maps". An assertion is a lightweight description of some fact about the state of the game
  (eg. "Marisa appears at the Forest of Magic, and if you talk to her, she tells you about mushrooms"),
 and a map is a collection of assertions that are all shown to the player at the same time (eg. in the above case,
  the Forest of Magic would be a map, since all assertions related to the Forest of Magic would appear to the player at the same time). 
Assertions are defined as a function of the game data (ADVData) by calling MapStateManager.ConfigureMap once for each map.
Of all the maps, only one is "realized" at a time, which is MapStateManager.CurrentMap. When a map is realized, all its
 assertions are actualized. When an assertion is actualized, the game state is changed to fit the description of the assertion.
 For example, EntityAssertion creates a VN entity when actualized (see EntityAssertion.ActualizeOnNewState). 
The code in EvidenceRequest supports requesting "evidence" (think Ace Attorney) from the player during dialogue.
 To do this, instantiate a new EvidenceRequest, and then use one of the following two methods:
- `using (var _ = evidenceRequest.Request(CONTINUTATION)) { ... }`
 In this case, the player can optionally present evidence while the code inside the brackets is being executed, 
 and if they do, the CONTINUATION function, which must be of type `Func<E, BoundedContext<InterruptionStatus>>`, 
 will be run on the provided evidence. After running some code, it should return either `InterruptionStatus.Continue` 
  (the nesting code should continue running) or `InterruptionStatus.Abort` (the nesting code should stop running). 
 Note that you cannot save or load within the CONTINUATION function, but if you make the CONTINUATION function a
 `StrongBoundedContext<InterruptionStatus>` with `LoadSafe = false`, then saving/loading within the CONTINUATION 
 function will send the player to the point right before they started the CONTINUATION function.
- `var ev = await evidenceRequester.WaitForEvidence(KEY)`. In this case, the player *must* present evidence to 
 continue the game execution. Save/load can still be used with this method, and KEY will be used to preserve the 
 value of the evidence provided when saving. (Note that your evidence type E must be serializable!)

*/