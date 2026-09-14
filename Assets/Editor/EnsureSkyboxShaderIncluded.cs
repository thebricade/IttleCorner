using UnityEditor;
using UnityEngine;

// The BecomeSky quest reward (SelectionScreen.ApplyAsSkybox) builds a skybox
// material via Shader.Find("Skybox/Panoramic") at runtime. Shaders that are
// only ever looked up by string like that get stripped out of player builds
// (iOS/Windows/WebGL/etc.) unless something in the project actually
// references them. This keeps the shader registered in Graphics Settings'
// "Always Included Shaders" list automatically, so the reward keeps working
// in builds without needing any scene or Inspector setup.
[InitializeOnLoad]
public static class EnsureSkyboxShaderIncluded
{
    private const string ShaderName = "Skybox/Panoramic";

    static EnsureSkyboxShaderIncluded()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            return;
        }

        Object graphicsSettings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
        if (graphicsSettings == null)
        {
            return;
        }

        SerializedObject serializedSettings = new SerializedObject(graphicsSettings);
        SerializedProperty alwaysIncluded = serializedSettings.FindProperty("m_AlwaysIncludedShaders");

        for (int i = 0; i < alwaysIncluded.arraySize; i++)
        {
            if (alwaysIncluded.GetArrayElementAtIndex(i).objectReferenceValue == shader)
            {
                return; // already registered
            }
        }

        alwaysIncluded.arraySize++;
        alwaysIncluded.GetArrayElementAtIndex(alwaysIncluded.arraySize - 1).objectReferenceValue = shader;
        serializedSettings.ApplyModifiedProperties();
    }
}
