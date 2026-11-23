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
        string inferencePath = Path.Combine(outputDirectory, "inference_reference.csv");
        Scene activeScene = SceneManager.GetActiveScene();
        StringBuilder json = new StringBuilder();
        StringBuilder csv = new StringBuilder();
        StringBuilder inference = new StringBuilder();

        json.AppendLine("{");
        json.AppendLine("  \"experiment_name\": \"" + escapeJson(NNLResultCaptureSettings.ExperimentName) + "\",");
        json.AppendLine("  \"captured_at\": \"" + DateTime.Now.ToString("o") + "\",");
        json.AppendLine("  \"scene\": \"" + escapeJson(scenePath) + "\",");
        json.AppendLine("  \"training_parameters\": \"" + escapeJson(NNLResultCaptureSettings.TrainingParameters) + "\",");
        json.AppendLine("  \"notes\": \"" + escapeJson(NNLResultCaptureSettings.ExperimentNotes) + "\",");
        json.AppendLine("  \"inference_reference\": \"CPU evaluation of captured 4-8-3 weights; not a GPU readback\",");
        json.AppendLine("  \"materials\": [");

        csv.AppendLine("material,texture,width,height,format,x,y,r,g,b,a");
        inference.AppendLine("material,sample,nx,ny,nz,ndotl,cpu_r,cpu_g,cpu_b,shaded_r,shaded_g,shaded_b");

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

                Vector3 lightDirection = getMainDirectionalLightDirection(activeScene);
                Vector3[] sampleNormals =
                {
                    Vector3.right,
                    Vector3.left,
                    Vector3.up,
                    Vector3.down,
                    Vector3.forward,
                    Vector3.back,
                    (Vector3.right + Vector3.up + Vector3.forward).normalized,
                    (Vector3.left + Vector3.up + Vector3.back).normalized
                };
                Color baseColor = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : Color.white;

                for (int sampleIndex = 0; sampleIndex < sampleNormals.Length; ++sampleIndex)
                {
                    Vector3 normal = sampleNormals[sampleIndex];
                    float ndotl = Vector3.Dot(normal, lightDirection);
                    Vector3 networkColor = evaluateNetwork(pixels, weights.width, weights.height, normal, ndotl);
                    Vector3 shadedColor = Vector3.Scale(networkColor, new Vector3(baseColor.r, baseColor.g, baseColor.b));

                    inference.Append(csvEscape(material.name)).Append(',')
                        .Append("normal_").Append(sampleIndex).Append(',')
                        .Append(normal.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(normal.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(normal.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(ndotl.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(networkColor.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(networkColor.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(networkColor.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(shadedColor.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(shadedColor.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(shadedColor.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
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
        File.WriteAllText(inferencePath, inference.ToString(), new UTF8Encoding(false));
    }

    private static Vector3 getMainDirectionalLightDirection(Scene scene)
    {
        Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
        for (int i = 0; i < lights.Length; ++i)
        {
            Light light = lights[i];
            if (light != null && light.type == LightType.Directional &&
                light.gameObject.scene == scene && light.isActiveAndEnabled)
                return -light.transform.forward.normalized;
        }

        return Vector3.forward;
    }

    private static Vector3 evaluateNetwork(Color[] pixels, int width, int height, Vector3 normal, float ndotl)
    {
        float[] hidden = new float[8];
        float[] input = { normal.x, normal.y, normal.z, ndotl };

        for (int neuron = 0; neuron < 8; ++neuron)
        {
            Color weights = pixels[neuron * width];
            float bias = pixels[neuron * width + 1].r;
            float sum = weights.r * input[0] + weights.g * input[1] +
                weights.b * input[2] + weights.a * input[3] + bias;
            hidden[neuron] = Mathf.Max(sum, 0.0f);
        }

        float[] output = new float[3];
        for (int channel = 0; channel < 3; ++channel)
        {
            int row = 8 + channel;
            Color weightsA = pixels[row * width];
            Color weightsB = pixels[row * width + 1];
            float bias = pixels[row * width + 2].r;
            float sum = weightsA.r * hidden[0] + weightsA.g * hidden[1] +
                weightsA.b * hidden[2] + weightsA.a * hidden[3] +
                weightsB.r * hidden[4] + weightsB.g * hidden[5] +
                weightsB.b * hidden[6] + weightsB.a * hidden[7] + bias;
            output[channel] = Mathf.Clamp01(sum);
        }

        return new Vector3(output[0], output[1], output[2]);
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
