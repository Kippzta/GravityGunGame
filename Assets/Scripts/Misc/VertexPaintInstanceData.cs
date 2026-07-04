using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
public class VertexPaintInstanceData : MonoBehaviour
{
    [SerializeField] private Mesh originalMesh;
    [SerializeField] private Color32[] storedColors;
    [SerializeField] private int storedVertexCount;
    [SerializeField] private bool hasStoredColors;

    private Mesh instanceMesh;

    public Mesh OriginalMesh => originalMesh;
    public bool HasStoredColors => hasStoredColors;
    public int StoredVertexCount => storedVertexCount;

    private void OnEnable()
    {
        if (originalMesh != null && hasStoredColors)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                ApplyToMeshFilter(meshFilter);
            }
        }
    }

    private void OnDestroy()
    {
        DestroyInstanceMesh();
    }

    public Mesh GetOrCreateInstanceMesh(MeshFilter meshFilter, Color32 defaultColor)
    {
        if (meshFilter == null)
        {
            return null;
        }

        if (originalMesh == null)
        {
            originalMesh = meshFilter.sharedMesh;
        }

        if (originalMesh == null)
        {
            return null;
        }

        EnsureStoredColors(defaultColor);

        if (instanceMesh == null || instanceMesh.vertexCount != originalMesh.vertexCount)
        {
            CreateInstanceMesh();
        }

        if (instanceMesh != null)
        {
            instanceMesh.colors32 = storedColors;
            meshFilter.sharedMesh = instanceMesh;
        }

        MarkDirty();

        return instanceMesh;
    }

    public void ApplyToMeshFilter(MeshFilter meshFilter)
    {
        if (meshFilter == null || originalMesh == null)
        {
            return;
        }

        EnsureStoredColors(Color.clear);

        if (instanceMesh == null || instanceMesh.vertexCount != originalMesh.vertexCount)
        {
            CreateInstanceMesh();
        }

        if (instanceMesh != null)
        {
            instanceMesh.colors32 = storedColors;
            meshFilter.sharedMesh = instanceMesh;
        }
    }

    public void StoreColors(Color32[] colors)
    {
        if (colors == null)
        {
            return;
        }

        storedColors = new Color32[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            storedColors[i] = colors[i];
        }

        storedVertexCount = storedColors.Length;
        hasStoredColors = true;

        if (instanceMesh != null && instanceMesh.vertexCount == storedColors.Length)
        {
            instanceMesh.colors32 = storedColors;
        }

        MarkDirty();
    }

    public Color32[] GetStoredColorsCopy(Color32 defaultColor)
    {
        EnsureStoredColors(defaultColor);

        Color32[] copy = new Color32[storedColors.Length];

        for (int i = 0; i < storedColors.Length; i++)
        {
            copy[i] = storedColors[i];
        }

        return copy;
    }

    public void ClearStoredColors(Color32 color)
    {
        if (originalMesh == null)
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                originalMesh = meshFilter.sharedMesh;
            }
        }

        if (originalMesh == null)
        {
            return;
        }

        storedColors = new Color32[originalMesh.vertexCount];

        for (int i = 0; i < storedColors.Length; i++)
        {
            storedColors[i] = color;
        }

        storedVertexCount = storedColors.Length;
        hasStoredColors = true;

        if (instanceMesh != null && instanceMesh.vertexCount == storedColors.Length)
        {
            instanceMesh.colors32 = storedColors;
        }

        MarkDirty();
    }

    public void RevertToOriginalMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter != null && originalMesh != null)
        {
            meshFilter.sharedMesh = originalMesh;
        }

        DestroyInstanceMesh();
        MarkDirty();
    }

    public void ResetPaintDataAndRevert()
    {
        RevertToOriginalMesh();

        storedColors = null;
        storedVertexCount = 0;
        hasStoredColors = false;

        MarkDirty();
    }

    private void EnsureStoredColors(Color32 defaultColor)
    {
        if (originalMesh == null)
        {
            return;
        }

        if (storedColors != null && storedColors.Length == originalMesh.vertexCount)
        {
            storedVertexCount = storedColors.Length;
            hasStoredColors = true;
            return;
        }

        Color32[] sourceColors = originalMesh.colors32;

        storedColors = new Color32[originalMesh.vertexCount];

        if (sourceColors != null && sourceColors.Length == originalMesh.vertexCount)
        {
            for (int i = 0; i < storedColors.Length; i++)
            {
                storedColors[i] = sourceColors[i];
            }
        }
        else
        {
            for (int i = 0; i < storedColors.Length; i++)
            {
                storedColors[i] = defaultColor;
            }
        }

        storedVertexCount = storedColors.Length;
        hasStoredColors = true;
    }

    private void CreateInstanceMesh()
    {
        DestroyInstanceMesh();

        if (originalMesh == null)
        {
            return;
        }

        instanceMesh = Instantiate(originalMesh);
        instanceMesh.name = originalMesh.name + "_SceneVertexPaintInstance";

        // Do not save the generated mesh object itself.
        // Only the color array on this component is saved in the scene/prefab override.
        instanceMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        if (storedColors != null && storedColors.Length == instanceMesh.vertexCount)
        {
            instanceMesh.colors32 = storedColors;
        }

        instanceMesh.UploadMeshData(false);
    }

    private void DestroyInstanceMesh()
    {
        if (instanceMesh == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(instanceMesh);
        }
        else
#endif
        {
            Destroy(instanceMesh);
        }

        instanceMesh = null;
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}