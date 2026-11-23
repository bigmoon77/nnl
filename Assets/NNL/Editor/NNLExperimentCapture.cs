using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NNLExperimentCapture
{
    public static void Write(string outputDirectory, string scenePath)
    {
        string jsonPath = Path.Combine(outputDirectory, "experiment.json");
        string csvPath = Path.Combine(outputDirectory, "weights.csv");
        Scene activeScene = SceneManager.GetActiveScene();
        StringBuilder json = new StringBuilder();
        StringBuilder csv = new StringBuilder();

        json.AppendLine("{");
        json.AppendLine("  \"experiment_name\": \"" + escapeJson(NNLResultCaptureSettings.ExperimentName) + "\",");
        json.AppendLine("  \"captured_at\": \"" + DateTime.Now.ToString("o") + "\",");
        json.AppendLine("  \"scene\": \"" + escapeJson(scenePath) + "\",");
        json.AppendLine("  \"training_parameters\": \"" + escapeJson(NNLResultCaptureSettings.TrainingParameters) + "\",");
        json.AppendLine("  \"notes\": \"" + escapeJson(NNLResultCaptureSettings.ExperimentNotes) + "\",");
        json.AppendLine("  \"materials\": [");

        csv.AppendLine("material,texture,width,height,format,x,y,r,g,b,a");

        List<Material> materials = new List<Material>();
        HashSet<int> materialIds = new HashSet<int>();
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();

        for (int rendererIndex = 0; rendererIndex < renderers.Length; ++rendererIndex)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != activeScene)
                continue;

            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < sharedMaterials.Length; ++materialIndex)
            {
                Material material = sharedMaterials[materialIndex];
                if (material == null || material.shader == null ||
                    !material.shader.name.StartsWith("NNL/", StringComparison.Ordinal))
                    continue;

                if (materialIds.Add(material.GetInstanceID()))
                    materials.Add(material);
            }
        }

        for (int materialIndex = 0; materialIndex < materials.Count; ++materialIndex)
        {
            Material material = materials[materialIndex];
            Texture2D weights = material.HasProperty("_NNLWeights")
                ? material.GetTexture("_NNLWeights") as Texture2D
                : null;

            json.AppendLine("    {");
            json.AppendLine("      \"name\": \"" + escapeJson(material.name) + "\",");
            json.AppendLine("      \"shader\": \"" + escapeJson(material.shader.name) + "\",");
            json.AppendLine("      \"base_color\": \"" +
                (material.HasProperty("_BaseColor") ? colorToString(material.GetColor("_BaseColor")) : "not_available") + "\",");
            json.AppendLine("      \"weights_texture\": " +
                (weights == null ? "null" : "\"" + escapeJson(weights.name) + "\"") + ",");
            json.AppendLine("      \"weights_readable\": " + (weights != null && weights.isReadable ? "true" : "false") + ",");
            json.AppendLine("      \"weights_width\": " + (weights == null ? 0 : weights.width) + ",");
            json.AppendLine("      \"weights_height\": " + (weights == null ? 0 : weights.height) + ",");
            json.AppendLine("      \"weights_format\": \"" + (weights == null ? "none" : weights.format.ToString()) + "\"");
            json.Append("    }");
            json.AppendLine(materialIndex + 1 < materials.Count ? "," : string.Empty);

            if (weights == null)
                continue;

            try
            {
                Color[] pixels = weights.GetPixels();
                for (int y = 0; y < weights.height; ++y)
                {
                    for (int x = 0; x < weights.width; ++x)
                    {
                        Color value = pixels[y * weights.width + x];
                        csv.Append(csvEscape(material.name)).Append(',')
                            .Append(csvEscape(weights.name)).Append(',')
                            .Append(weights.width).Append(',')
                            .Append(weights.height).Append(',')
                            .Append(weights.format).Append(',')
                            .Append(x).Append(',')
                            .Append(y).Append(',')
                            .Append(value.r.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                            .Append(value.g.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                            .Append(value.b.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                            .Append(value.a.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
                    }
                }
            }
            catch (Exception exception)
            {
                csv.Append("# Could not read ").Append(csvEscape(material.name))
                    .Append(": ").AppendLine(csvEscape(exception.Message));
            }
        }

        json.AppendLine("  ],");
        json.AppendLine("  \"material_count\": " + materials.Count);
        json.AppendLine("}");
        File.WriteAllText(jsonPath, json.ToString(), new UTF8Encoding(false));
        File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
    }

    private static string escapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static string csvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string colorToString(Color color)
    {
        return color.r.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
            color.g.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
            color.b.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
            color.a.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
