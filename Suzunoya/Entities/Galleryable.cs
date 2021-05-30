using System;
using System.Numerics;
using BagoumLib.DataStructures;

namespace Suzunoya.Entities {
public interface IGalleryable {
    public string Key { get; }
}

public class GalleryCG : Rendered, IGalleryable {
    public string Key { get; }
    
    public GalleryCG(string key, bool visible = false, FColor? color = null) : base(null, null, null, visible, color) {
        Key = key;
        Visible.Subscribe(b => {
            if (b)
                Container.RecordCG(this);
        });
    }
}

}