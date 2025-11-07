#if TOOLS
using Godot;
using System;
using System.Linq;
using Godot.Collections;

namespace Parallas.MeshText;
[Tool]
public partial class MeshTextGizmoPlugin : EditorNode3DGizmoPlugin
{
	public override string _GetGizmoName()
	{
		return "Text3D";
	}

	public override bool _HasGizmo(Node3D forNode3D)
	{
		return forNode3D is MeshText;
	}

	internal MeshTextGizmoPlugin()
	{
		CreateMaterial("main", new Color(1, 1, 1));
		CreateHandleMaterial("handles");
	}

	public override void _Redraw(EditorNode3DGizmo gizmo)
	{
		base._Redraw(gizmo);

		gizmo.Clear();

		var node3d = gizmo.GetNode3D();
		if (node3d is not MeshText text3D) return;

		var rectPositions = text3D.GetGizmoRectPositions();
		Vector3[] lines = [
			rectPositions[0], rectPositions[1],
			rectPositions[1], rectPositions[2],
			rectPositions[2], rectPositions[3],
			rectPositions[3], rectPositions[0],
		];

		gizmo.AddLines(lines, GetMaterial("main", gizmo), false);

		// var handles = PackedVector3Array()
		//
		// handles.push_back(Vector3(0, 1, 0))
		// handles.push_back(Vector3(0, node3d.my_custom_value, 0))
		// gizmo.add_handles(handles, get_material("handles", gizmo), [])

	}
}
#endif
