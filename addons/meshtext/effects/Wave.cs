using Godot;
using System;

namespace Parallas.MeshText.Effects;
[GlobalClass, Tool]
public partial class Wave : MeshTextEffect
{
    [Export] public float Speed = 1f;
    [Export] public float IndexOffset = 0f;
    [Export] public float Intensity = 0.2f;
    [Export] public bool Bounce = false;

    public override Transform3D UpdateRelativeTransform(Rid instance, int index, Transform3D transform, float time, double delta)
    {
        float waveValue = Mathf.Sin(time * Speed + IndexOffset * index);
        if (Bounce) waveValue = Mathf.Abs(waveValue);
        return transform.TranslatedLocal(Vector3.Up * waveValue * Intensity);
    }
}
