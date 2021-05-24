using System.Numerics;

namespace Suzunoya.Display {
public class DisplayConfig {
    /// <summary>
    /// In (-1,1) screen coordinates, the location on the screen that is targeted by Zoom.
    /// </summary>
    private Vector2 ZoomCenter = new Vector2(0, 0);
    /// <summary>
    /// The amount of zoom to apply in each coordinate axis.
    /// </summary>
    private Vector3 Zoom = new Vector3(1, 1, 1);
    

}
}