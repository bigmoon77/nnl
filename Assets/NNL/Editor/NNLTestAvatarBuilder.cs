using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NNLTestAvatarBuilder
{
    private const string RootPath = "Assets/NNL";
    private const string GeneratedPath = RootPath + "/Generated";
    private const string MaterialPath = GeneratedPath + "/NNL_Test.mat";
    private const string GroundMaterialPath = GeneratedPath + "/NNL_Ground.mat";
    private const string WeightsPath = GeneratedPath + "/NNL_TestWeights.asset";
    private const string ScenePath = RootPath + "/Scenes/NNL_TestAvatar.unity";
    private const string ShaderName = "NNL/Unlit MLP";

    [MenuItem("NNL/Build Test Avatar")]
    public static void BuildTestAvatar()
    {
        EnsureDirectory(RootPath);
        EnsureDirectory(GeneratedPath);
        EnsureDirectory(RootPath + "/Scenes");

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError("NNL shader was not found. Make sure NNL_UnlitMLP.shader has imported successfully.");
            return;
        }

        Texture2D weights = CreateOrReplaceWeights();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        material.SetTexture("_NNLWeights", weights);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject avatar = new GameObject("NNL_TestAvatar");
        avatar.transform.position = Vector3.zero;

        CreatePart(avatar.transform, "Torso", PrimitiveType.Capsule,
            new Vector3(0.0f, 1.25f, 0.0f), new Vector3(0.48f, 0.65f, 0.28f), Quaternion.identity, material);
        CreatePart(avatar.transform, "Head", PrimitiveType.Sphere,
            new Vector3(0.0f, 2.18f, 0.0f), new Vector3(0.34f, 0.38f, 0.32f), Quaternion.identity, material);
        CreatePart(avatar.transform, "LeftArm", PrimitiveType.Capsule,
            new Vector3(-0.58f, 1.32f, 0.0f), new Vector3(0.16f, 0.48f, 0.16f),
            Quaternion.Euler(0.0f, 0.0f, 18.0f), material);
        CreatePart(avatar.transform, "RightArm", PrimitiveType.Capsule,
            new Vector3(0.58f, 1.32f, 0.0f), new Vector3(0.16f, 0.48f, 0.16f),
            Quaternion.Euler(0.0f, 0.0f, -18.0f), material);
        CreatePart(avatar.transform, "LeftLeg", PrimitiveType.Capsule,
            new Vector3(-0.19f, 0.48f, 0.0f), new Vector3(0.19f, 0.52f, 0.19f), Quaternion.identity, material);
        CreatePart(avatar.transform, "RightLeg", PrimitiveType.Capsule,
            new Vector3(0.19f, 0.48f, 0.0f), new Vector3(0.19f, 0.52f, 0.19f), Quaternion.identity, material);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0.0f, -0.08f, 0.0f);
        ground.transform.localScale = new Vector3(4.0f, 1.0f, 4.0f);
        Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (groundMaterial == null)
        {
            groundMaterial = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(groundMaterial, GroundMaterialPath);
        }
        groundMaterial.color = new Color(0.22f, 0.24f, 0.27f);
        EditorUtility.SetDirty(groundMaterial);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        GameObject lightObject = new GameObject("Directional Light");
        Light directionalLight = lightObject.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(35.0f, -35.0f, 0.0f);

        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 35.0f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100.0f;
        cameraObject.transform.position = new Vector3(3.2f, 2.5f, -6.0f);
        cameraObject.transform.LookAt(new Vector3(0.0f, 1.15f, 0.0f));

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.22f);
        Selection.activeGameObject = avatar;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("NNL test avatar scene created: " + ScenePath);
    }

    private static Texture2D CreateOrReplaceWeights()
    {
        AssetDatabase.DeleteAsset(WeightsPath);

        // Layout: 3 x 11 RGBAFloat texels.
        // Rows 0..7: first-layer weights in x=0 and bias in x=1.r.
        // Rows 8..10: output weights in x=0..1 and bias in x=2.r.
        Texture2D texture = new Texture2D(3, 11, TextureFormat.RGBAFloat, false, true)
        {
            name = "NNL_TestWeights",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };

        // Hidden units encode positive/negative normal axes and positive N.L.
        Vector4[] firstLayer =
        {
            new Vector4( 1,  0,  0, 0),
            new Vector4(-1,  0,  0, 0),
            new Vector4( 0,  1,  0, 0),
            new Vector4( 0, -1,  0, 0),
            new Vector4( 0,  0,  1, 0),
            new Vector4( 0,  0, -1, 0),
            new Vector4( 0,  0,  0, 1),
            Vector4.zero
        };

        for (int row = 0; row < 11; ++row)
        {
            texture.SetPixel(0, row, Color.clear);
            texture.SetPixel(1, row, Color.clear);
            texture.SetPixel(2, row, Color.clear);
        }

        for (int row = 0; row < 8; ++row)
        {
            Vector4 weights = firstLayer[row];
            texture.SetPixel(0, row, new Color(weights.x, weights.y, weights.z, weights.w));
        }

        // RGB output: 0.5 + 0.3 * normal component + 0.25 * max(N.L, 0).
        SetOutputWeights(texture, 8, 0);
        SetOutputWeights(texture, 9, 1);
        SetOutputWeights(texture, 10, 2);

        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, WeightsPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(WeightsPath);
    }

    private static void SetOutputWeights(Texture2D texture, int row, int channel)
    {
        Vector4 weightsA = Vector4.zero;
        Vector4 weightsB = Vector4.zero;

        if (channel == 0)
        {
            weightsA.x = 0.3f;
            weightsA.y = -0.3f;
        }
        else if (channel == 1)
        {
            weightsA.z = 0.3f;
            weightsA.w = -0.3f;
        }
        else
        {
            weightsB.x = 0.3f;
            weightsB.y = -0.3f;
        }

        weightsB.z = 0.25f;
        texture.SetPixel(0, row, new Color(weightsA.x, weightsA.y, weightsA.z, weightsA.w));
        texture.SetPixel(1, row, new Color(weightsB.x, weightsB.y, weightsB.z, weightsB.w));
        texture.SetPixel(2, row, new Color(0.5f, 0.0f, 0.0f, 0.0f));
    }

    private static void CreatePart(
        Transform parent,
        string partName,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.transform.localRotation = localRotation;
        part.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
