using Godot;
using System;

namespace Parallas.MeshText;
[GlobalClass, Tool]
public abstract partial class MeshTextEffect : Resource
{
    public virtual Transform3D UpdateRelativeTransform(Rid instance, int index, Transform3D transform, float time, double delta)
    {
        return Transform3D.Identity;
    }
}
