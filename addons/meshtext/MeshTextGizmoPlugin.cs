#if TOOLS
using Godot;
using System;
using System.Linq;
using Godot.Collections;

namespace Parallas.MeshText;
[Tool]
public partial class MeshTextGizmoPlugin : EditorNode3DGizmoPlugin
{
	public EditorUndoRedoManager UndoRedo;
	private int _maxCharacterWidth;
	private int _newMaxCharacterWidth;
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

		Vector3[] handles =
		[
			rectPositions[0].Lerp(rectPositions[1], 0.5f), // right edge
			rectPositions[2].Lerp(rectPositions[3], 0.5f), // left edge
			// rectPositions[1].Lerp(rectPositions[2], 0.5f), // bottom edge
			// rectPositions[3].Lerp(rectPositions[0], 0.5f), // top edge
			// rectPositions[0],
			// rectPositions[1],
			// rectPositions[2],
			// rectPositions[3],
		];
		gizmo.AddHandles(handles, GetMaterial("handles", gizmo), []);
	}

	public override void _BeginHandleAction(EditorNode3DGizmo gizmo, int handleId, bool secondary)
	{
		base._BeginHandleAction(gizmo, handleId, secondary);
		if (gizmo.GetNode3D() is not MeshText meshText) return;
		_maxCharacterWidth = meshText.MaxCharacterWidth;
		_newMaxCharacterWidth = _maxCharacterWidth;
	}

	public override void _SetHandle(EditorNode3DGizmo gizmo, int handleId, bool secondary, Camera3D camera, Vector2 screenPos)
	{
		base._SetHandle(gizmo, handleId, secondary, camera, screenPos);
		if (gizmo.GetNode3D() is not MeshText meshText) return;
		var plane = new Plane(meshText.GlobalBasis.Z, meshText.GlobalPosition);
		var intersectionTest = plane.IntersectsRay(camera.ProjectRayOrigin(screenPos), camera.ProjectRayNormal(screenPos));
		if (intersectionTest is not { } intersectionPoint) return;

		if (handleId is 0 or 1)
		{
			int changeAmount = Mathf.RoundToInt(meshText.ToLocal(intersectionPoint).X / meshText.FontSize);
			int flip = handleId == 1 ? -1 : 1;
			_newMaxCharacterWidth = Mathf.Abs(changeAmount * 2 * flip);
			meshText.MaxCharacterWidth = _newMaxCharacterWidth;
		}
	}

	public override void _CommitHandle(EditorNode3DGizmo gizmo, int handleId, bool secondary, Variant restore, bool cancel)
	{
		base._CommitHandle(gizmo, handleId, secondary, restore, cancel);
		if (gizmo.GetNode3D() is not MeshText meshText) return;
		if (handleId is 0 or 1)
		{
			UndoRedo.CreateAction($"Set MaxCharacterWidth");
			UndoRedo.AddDoProperty(meshText, "MaxCharacterWidth", _newMaxCharacterWidth);
			UndoRedo.AddUndoProperty(meshText, "MaxCharacterWidth", _maxCharacterWidth);
			UndoRedo.CommitAction();
		}
	}
}
#endif
