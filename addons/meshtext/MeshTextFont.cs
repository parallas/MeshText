using Godot;
using System;
using Godot.Collections;

namespace Parallas.MeshText;
[GlobalClass, Tool, Icon("res://addons/meshtext/icons/MeshTextFont.svg")]
public partial class MeshTextFont : Resource
{
    [Export] public String CharacterSet;
    [Export] public Array<Mesh> CharacterMeshes = new Array<Mesh>();
    [Export] public Dictionary<String, Mesh> SubstringMeshes = new Dictionary<String, Mesh>();
    [Export] public bool CaseSensitive = true;

    public bool TryGetMeshForCharacter(char character, out Mesh mesh)
    {
        mesh = null;
        int index = CharacterSet.IndexOf(character);
        if (index == -1) return false;
        if (index >= CharacterMeshes.Count) return false;
        mesh = CharacterMeshes[index];
        return true;
    }

    public bool TryGetMeshForSubstring(String substring, out Mesh mesh) => SubstringMeshes.TryGetValue(substring, out mesh);
}
