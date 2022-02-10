using System.Collections;
using BagoumLib.Cancellation;

namespace BagoumLib {
public interface IUpdateable {
    void Update(float dT);

    IEnumerator AsCoroutine(float dT, ICancellee? cT = null) {
        if (cT?.Cancelled == true)
            yield break;
        while (true) {
            yield return null;
            if (cT?.Cancelled == true)
                yield break;
            Update(dT);
        }
    }
}
}