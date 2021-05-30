using BagoumLib.Cancellation;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;

namespace Suzunoya {
//These are examples of how to extend the basic handling of components with game-specific features.
//You can alternatively reimplement the components.

public class SZYCharacter : Character {
}

public class SZYDialogueBox : DialogueBox {
    
}

public class SZYVNState : VNState {
    public SZYVNState(ICancellee extCToken) : base(extCToken) { }
}

//this can subtype monobehavior or whatever engine-specific class
public class SZYUnityCharacter {
    private SZYCharacter MyCharacter;
    
    public SZYUnityCharacter(SZYCharacter myCharacter) {
        MyCharacter = myCharacter;
        //link events to gameobject changes like this.
        //MyCharacter.Location.Subscribe(x => tr.localPosition = x);
    }
}


}