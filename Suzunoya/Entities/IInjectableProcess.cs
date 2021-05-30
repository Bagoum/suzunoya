namespace Suzunoya.Entities {

//Consider the case of writing a script where, halfway through, we start running some game-specific minigame.
// The minigame reports some reduced results (or maybe it reports nothing at all) that is saved into save data.
// However, the state of the minigame is not reported.
//Eg. consider tic tac toe, where the winner is reported through `await GameObject.Instantiate(ticTacToeGame).Play(cT)`,
// but the state of the board is not.
//If the VN performs a load that stops one step after the game result is reported, *but before the visible object is removed*,
// then how does the VNState retrieve and later inform the minigame about its state at the time of saving? If there
// are two consecutive dependent games with a VN operation in between, the state *must* be preserved.
//Furthermore, if the minigame is run far before the save point, how does the load process indicate to the minigame that its
// entry and possibly deletion can be reduced? ie. how do we ensure the minigame respects softSkip semantics?

//Note: see IInterrogator for an implementation of the end-state only saving.
// I think you should simply supplement that object-delegation style with more saved state,
// though I think you will need more handling to support the *partial* state (eg. two consecutive minigames on a continuous board).

public interface IInjectableProcess {
    object SaveState();
    void LoadState(object st);
}
}