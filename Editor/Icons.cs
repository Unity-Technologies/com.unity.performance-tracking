using System;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;

namespace Unity.PerformanceTracking
{
    public static class Icons
    {
        public static string iconFolder = $"{Utils.packageFolderName}/Editor/Icons";
        public static Texture2D settings = LoadIcon($"{iconFolder}/settings.png");

        static Icons()
        {
            if (EditorGUIUtility.isProSkin)
            {
                settings = LightenTexture(settings);
            }
        }

        private static Texture2D LoadIcon(string resourcePath, bool autoScale = false)
        {
            if (String.IsNullOrEmpty(resourcePath))
                return null;

            float systemScale = EditorGUIUtility.pixelsPerPoint;
            if (autoScale && systemScale > 1f)
            {
                int scale = Mathf.RoundToInt(systemScale);
                string dirName = Path.GetDirectoryName(resourcePath).Replace('\\', '/');
                string fileName = Path.GetFileNameWithoutExtension(resourcePath);
                string fileExt = Path.GetExtension(resourcePath);
                for (int s = scale; scale > 1; --scale)
                {
                    string scaledResourcePath = $"{dirName}/{fileName}@{s}x{fileExt}";
                    var scaledResource = EditorResources.Load<Texture2D>(scaledResourcePath, false);
                    if (scaledResource)
                        return scaledResource;
                }
            }

            return EditorResources.Load<Texture2D>(resourcePath, false);
        }

        private static Texture2D LightenTexture(Texture2D texture)
        {
            if (!texture)
                return texture;
            Texture2D outTexture = new Texture2D(texture.width, texture.height);
            var outColorArray = outTexture.GetPixels();

            var colorArray = texture.GetPixels();
            for (var i = 0; i < colorArray.Length; ++i)
                outColorArray[i] = LightenColor(colorArray[i]);

            outTexture.hideFlags = HideFlags.HideAndDontSave;
            outTexture.SetPixels(outColorArray);
            outTexture.Apply();

            return outTexture;
        }

        public static Color LightenColor(Color color)
        {
            Color.RGBToHSV(color, out var h, out _, out _);
            var outColor = Color.HSVToRGB((h + 0.5f) % 1, 0f, 0.8f);
            outColor.a = color.a;
            return outColor;
        }
    }
}