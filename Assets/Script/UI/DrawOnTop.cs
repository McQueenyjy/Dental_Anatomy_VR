using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Makes all UI under this object draw on top of 3D content (the tooth, highlights).
// Images get the on-top UI material; texts get an on-top copy of their font material.
public class DrawOnTop : MonoBehaviour
{
    [Tooltip("UI material that ignores depth, e.g. UI_Overlay (shader: UI/Overlay On Top).")]
    public Material imageMaterial;
    [Tooltip("TextMeshPro shader that ignores depth: TextMeshPro/Distance Field Overlay.")]
    public Shader textShader;

    // One on-top copy per original font material, shared by all texts using it.
    private static readonly Dictionary<Material, Material> textMaterials = new Dictionary<Material, Material>();

    private void Awake() => Apply();

    public void Apply()
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is TMP_Text text)
            {
                Material source = text.fontSharedMaterial;
                if (source == null || textShader == null || source.shader == textShader) continue;
                if (!textMaterials.TryGetValue(source, out Material onTop) || onTop == null)
                {
                    onTop = new Material(source) { shader = textShader, name = source.name + " (On Top)" };
                    textMaterials[source] = onTop;
                }
                text.fontSharedMaterial = onTop;
            }
            else if (imageMaterial != null)
            {
                graphic.material = imageMaterial;
            }
        }
    }
}