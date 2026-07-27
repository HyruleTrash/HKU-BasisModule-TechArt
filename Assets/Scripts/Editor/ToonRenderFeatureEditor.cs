using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ToonRenderFeature))]
public class ToonRenderFeatureEditor : Editor
{
    private static readonly int VolumeTexID = Shader.PropertyToID("_VolumeTex");
    
    private Material previewMaterial;
    private RenderTexture preview2DTarget;

    private void OnEnable()
    {
        Shader shader = Shader.Find("Hidden/ColorCountWheelPreview");
        if (shader) this.previewMaterial = new Material(shader);
    }

    private void OnDisable()
    {
        if (this.previewMaterial) DestroyImmediate(this.previewMaterial);

        if (!this.preview2DTarget) return;
        this.preview2DTarget.Release();
        DestroyImmediate(this.preview2DTarget);
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ToonRenderFeature feature = (ToonRenderFeature)this.target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Exact Color Distribution Matrix (RG | RB)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to view the live color distribution matrix.", MessageType.Info);
            return;
        }

        RenderTexture volumeTex = feature.ColorCountTexture;
        if (!volumeTex || !volumeTex.IsCreated())
        {
            EditorGUILayout.HelpBox("Color count texture is not allocated or initialized.", MessageType.Warning);
            return; 
        }

        if (!this.preview2DTarget || !this.preview2DTarget.IsCreated())
        {
            this.preview2DTarget = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            this.preview2DTarget.Create();
        }

        if (!this.previewMaterial) return;
        if (Event.current.type == EventType.Repaint)
        {
            this.previewMaterial.SetTexture(VolumeTexID, volumeTex);

            RenderTexture previousRT = RenderTexture.active;
            RenderTexture.active = this.preview2DTarget;
            Graphics.Blit(null, this.preview2DTarget, this.previewMaterial);
            RenderTexture.active = previousRT;
        }

        EditorGUILayout.Space(5);
            
        Rect rect = EditorGUILayout.GetControlRect(false, 256);
        rect.x = (EditorGUIUtility.currentViewWidth - 256) * 0.5f;
        rect.width = 256;
        rect.height = 256;

        if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, this.preview2DTarget);

        GUILayout.Space(260);

        EditorGUILayout.LabelField("Left: Red (X) vs Green (Y) | Right: Red (X) vs Blue (Y)");
    }
}