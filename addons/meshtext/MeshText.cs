using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot.Collections;
using Range = System.Range;

namespace Parallas.MeshText;
[GlobalClass, Tool, Icon("res://addons/meshtext/icons/MeshText.svg")]
public partial class MeshText : Node3D, ISerializationListener
{
    [Signal] public delegate void TextChangedEventHandler(string text);
    [Signal] public delegate void FontChangedEventHandler(MeshTextFont font);
    [Signal] public delegate void MaterialOverrideChangedEventHandler(Material material);

    [Export(PropertyHint.MultilineText)]
    public String Text
    {
        get => _text;
        set
        {
            _text = value;
            GenerateText();
            EmitSignalTextChanged(value);
        }
    }
    private String _text;

    [Export]
    public MeshTextFont Font
    {
        get => _font;
        set
        {
            _font = value;
            GenerateText();
            EmitSignalFontChanged(value);
        }
    }
    private MeshTextFont _font;

    [ExportGroup("Visual Settings")]
    [Export] public Color Tint = Colors.White;
    [Export] public float FontSize = 1f;

    [Export]
    public Material MaterialOverride
    {
        get => _materialOverride;
        set
        {
            _materialOverride = value;
            GenerateText();
            EmitSignalMaterialOverrideChanged(value);
        }
    }
    private Material _materialOverride = null;

    [ExportGroup("Spacing")]
    [Export] public float CharacterSpacing = 1f;
    [Export] public float LineSpacing = 1f;

    [ExportGroup("Alignment")]
    [Export] public AlignmentHorizontal HorizontalAlignment = AlignmentHorizontal.Left;
    [Export] public AlignmentVertical VerticalAlignment = AlignmentVertical.Top;
    [Export] public AlignmentHorizontal HorizontalJustification = AlignmentHorizontal.Left;

    [ExportGroup("Max Character Width")]
    [Export(PropertyHint.GroupEnable)] public bool UseMaxCharacterWidth = false;
    [Export(PropertyHint.Range, "1, 2147483647")] public int MaxCharacterWidth = 16;
    private int EvaluatedMaxCharacterWidth => UseMaxCharacterWidth ? MaxCharacterWidth : Text.Length;
    [Export] public bool WordWrap = true;

    [ExportGroup("Effects")]
    [Export] public Array<MeshTextEffect> TextEffects = new();

    private List<Rid> _instances = new();
    public System.Collections.Generic.Dictionary<int, Rid> CharacterIndexInstances { get; private set; } = new();
    public System.Collections.Generic.Dictionary<Rid, Vector2I> CharacterPositions { get; private set; } = new();
    public System.Collections.Generic.Dictionary<Rid, Transform3D> Transforms { get; private set; } = new();
    public System.Collections.Generic.Dictionary<Rid, Transform3D> RelativeTransforms { get; private set; } = new();
    public float[] HorizontalOffsets { get; private set; } = [];

    private float AlignmentOffsetX => HorizontalAlignment switch
    {
        AlignmentHorizontal.Left => 0f,
        AlignmentHorizontal.Center => -EvaluatedMaxCharacterWidth * 0.5f,
        AlignmentHorizontal.Right => -EvaluatedMaxCharacterWidth,
        _ => 0f
    };
    private float AlignmentOffsetY => VerticalAlignment switch
    {
        AlignmentVertical.Top => 0f,
        AlignmentVertical.Center => -HorizontalOffsets.Length * 0.5f,
        AlignmentVertical.Bottom => -HorizontalOffsets.Length,
        _ => 0f
    };

    private RegEx _wordSplitRegex = new RegEx();
    private float _time = 0f;

    public enum AlignmentHorizontal
    {
        Left,
        Center,
        Right
    }

    public enum AlignmentVertical
    {
        Top,
        Center,
        Bottom
    }

    public override void _Ready()
    {
        base._Ready();
        _wordSplitRegex.Compile(@"(\s+)|(\S+)");
    }

    public override void _EnterTree()
    {
        base._EnterTree();

        GenerateText();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        ClearText();
    }

    public void OnBeforeSerialize()
    {
        ClearText();
    }

    public void OnAfterDeserialize()
    {
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        _time += (float)delta;
        ProcessTransformChanges(delta);
    }

    private void GenerateText()
    {
        ClearText();

        if (Font is null) return;
        if (!IsInstanceValid(this)) return;
        if (GetWorld3D() is not { } world3d) return;

        for (int i = 0; i < Text.Length; i++)
        {
            char c = Text[i];
            if (Font.TryGetMeshForCharacter(c, out Mesh mesh))
            {
                // Create a visual instance (for 3D).
                var instance = RenderingServer.InstanceCreate();
                // Set the scenario from the world, this ensures it
                // appears with the same objects as the scene.
                Rid scenario = world3d.Scenario;
                RenderingServer.InstanceSetScenario(instance, scenario);
                RenderingServer.InstanceSetBase(instance, mesh.GetRid());
                if (_materialOverride is not null && IsInstanceValid(_materialOverride))
                    RenderingServer.InstanceSetSurfaceOverrideMaterial(instance, 0, _materialOverride.GetRid());

                // create the necessary transform dictionary entries and set them on the rendering server
                CreateTransform(instance);

                _instances.Add(instance);
                CharacterIndexInstances.Add(i, instance);
            }
        }
    }

    private void ClearText()
    {
        foreach (var instance in _instances)
        {
            RenderingServer.FreeRid(instance);
        }

        _instances.Clear();
        HorizontalOffsets = [];
        CharacterIndexInstances.Clear();
        CharacterPositions.Clear();
        Transforms.Clear();
        RelativeTransforms.Clear();
    }

    private void CreateTransform(Rid instance)
    {
        RelativeTransforms[instance] = Transform3D.Identity;
        Transforms[instance] = Transform3D.Identity;
        RenderingServer.InstanceSetTransform(instance, Transform3D.Identity);
    }

    private Transform3D UpdateInstanceTransform(Rid instance)
    {
        var characterPosition = CharacterPositions[instance];
        var horizontalOffset = HorizontalOffsets[characterPosition.Y];
        Vector3 spacingOffsets = Vector3.Zero;
        if (HorizontalAlignment == AlignmentHorizontal.Center) spacingOffsets.X = (CharacterSpacing - 1) * 0.5f;
        if (HorizontalAlignment == AlignmentHorizontal.Right) spacingOffsets.X = CharacterSpacing - 1;

        if (VerticalAlignment == AlignmentVertical.Center) spacingOffsets.Y = -(LineSpacing - 1) * 0.5f;
        if (VerticalAlignment == AlignmentVertical.Bottom) spacingOffsets.Y = -(LineSpacing - 1);
        Transform3D xform = GlobalTransform
                .ScaledLocal(Vector3.One * FontSize)
                .TranslatedLocal(new Vector3(CharacterSpacing * 0.5f, -LineSpacing * 0.5f, 0f))
                .TranslatedLocal(spacingOffsets)
                .TranslatedLocal(Vector3.Right * (characterPosition.X + horizontalOffset) * CharacterSpacing)
                .TranslatedLocal(Vector3.Down * (characterPosition.Y + AlignmentOffsetY) * LineSpacing)
                .TranslatedLocal(Vector3.Left * (CharacterSpacing - 1f) * 0.5f)
                .TranslatedLocal(Vector3.Up * (LineSpacing - 1f) * 0.5f)
            ;
        Transforms[instance] = xform;
        var finalTransform = Transforms[instance] * RelativeTransforms[instance];
        RenderingServer.InstanceSetTransform(instance, finalTransform);
        return finalTransform;
    }

    private void ProcessTransformChanges(double delta)
    {
        // apply all text effects to the reset relative transform
        for (int i = 0; i < _instances.Count; i++)
        {
            var instance = _instances[i];
            RelativeTransforms[instance] = Transform3D.Identity;
            var relativeTransform = RelativeTransforms[instance];
            foreach (var text3DEffect in TextEffects)
            {
                if (text3DEffect is null) continue;
                relativeTransform = text3DEffect.UpdateRelativeTransform(instance, i, relativeTransform, _time, delta);
            }
            RelativeTransforms[instance] = relativeTransform;
        }

        String[] words;
        if (WordWrap)
            words = [.._wordSplitRegex.SearchAll(Text).SelectMany<RegExMatch, String>(match => [match.Strings[1], match.Strings[2]])];
        else
            words = [..Text.ToCharArray().Select(c => c.ToString())];
        List<String> lines = [];
        StringBuilder currentLine = new StringBuilder();
        var currentWidth = 0;
        foreach (var word in words)
        {
            var wordWidth = word.Length;
            var wordWidthTrimmed = word.TrimEnd().Length;
            if (currentWidth + wordWidthTrimmed > EvaluatedMaxCharacterWidth)
            {
                if (currentWidth > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentWidth = 0;
                }
                else
                {
                    lines.Add(word);
                    continue;
                }
            }

            currentLine.Append(word);
            currentWidth += wordWidth;
        }
        if (currentWidth > 0)
        {
            lines.Add(currentLine.ToString());
        }
        HorizontalOffsets = new float[lines.Count];

        int charCounter = 0;
        Vector2I characterPos = Vector2I.Zero;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineTrimWidth = line.Trim().Length;
            var offset = EvaluatedMaxCharacterWidth - lineTrimWidth;

            float positionOffsetX = HorizontalJustification switch
            {
                AlignmentHorizontal.Left => 0,
                AlignmentHorizontal.Center => offset * 0.5f,
                AlignmentHorizontal.Right => offset,
                _ => 0f
            };
            HorizontalOffsets[i] = positionOffsetX + AlignmentOffsetX;

            for (int charIndex = 0; charIndex < line.Length; charIndex++)
            {
                bool hasCharacter = CharacterIndexInstances.TryGetValue(charCounter, out Rid instance);
                if (hasCharacter)
                {
                    CharacterPositions[instance] = characterPos;
                    UpdateInstanceTransform(instance);
                    RenderingServer.InstanceSetVisible(instance, Visible);
                    RenderingServer.InstanceGeometrySetShaderParameter(instance, "tint", Tint);
                }

                characterPos.X++;
                charCounter++;
            }

            characterPos.X = 0;
            characterPos.Y++;
        }

        #if TOOLS
        UpdateGizmos();
        #endif
    }

    #if TOOLS
    public Vector3[] GetGizmoRectPositions()
    {
        float charSpacingPad = (CharacterSpacing - 1f) * 0.5f;
        float lineSpacingPad = (LineSpacing - 1f) * 0.5f;

        float padOffsetX = HorizontalAlignment switch
        {
            AlignmentHorizontal.Left => -charSpacingPad,
            AlignmentHorizontal.Center => 0f,
            AlignmentHorizontal.Right => charSpacingPad,
            _ => 0f
        };
        float padOffsetY = VerticalAlignment switch
        {
            AlignmentVertical.Top => lineSpacingPad,
            AlignmentVertical.Center => 0f,
            AlignmentVertical.Bottom => -lineSpacingPad,
            _ => 0f
        };

        var translation = Vector3.Zero;
        var sideOffsetX = EvaluatedMaxCharacterWidth * CharacterSpacing * 0.5f;
        if (HorizontalAlignment == AlignmentHorizontal.Left) translation.X = sideOffsetX;
        if (HorizontalAlignment == AlignmentHorizontal.Right) translation.X = -sideOffsetX;
        translation.X += padOffsetX;

        var sideOffsetY = HorizontalOffsets.Length * LineSpacing * 0.5f;
        if (VerticalAlignment == AlignmentVertical.Top) translation.Y = -sideOffsetY;
        if (VerticalAlignment == AlignmentVertical.Bottom) translation.Y = sideOffsetY;
        translation.Y += padOffsetY;

        var scale = Vector3.One;
        scale.X = EvaluatedMaxCharacterWidth * CharacterSpacing - charSpacingPad * 2f;
        scale.Y = HorizontalOffsets.Length * LineSpacing - lineSpacingPad * 2f;
        Transform3D finalTransform = Transform3D.Identity
                .Scaled(Vector3.One * FontSize)
                .Translated(translation)
                .Scaled(scale)
            ;

        return
        [
            finalTransform * new Vector3(0.5f, 0.5f, 0f),
            finalTransform * new Vector3(0.5f, -0.5f, 0f),
            finalTransform * new Vector3(-0.5f, -0.5f, 0f),
            finalTransform * new Vector3(-0.5f, 0.5f, 0f),
        ];
    }
    #endif
}
