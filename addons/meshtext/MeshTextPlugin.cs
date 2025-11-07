#if TOOLS
using Godot;
using System;
using System.Linq;
using Godot.Collections;

namespace Parallas.MeshText;
[Tool]
public partial class MeshTextPlugin : EditorPlugin
{
    private MeshTextGizmoPlugin _gizmoPlugin = new MeshTextGizmoPlugin();
    public override void _EnterTree()
    {
        base._EnterTree();
        AddNode3DGizmoPlugin(_gizmoPlugin);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        RemoveNode3DGizmoPlugin(_gizmoPlugin);
    }
}
#endif
