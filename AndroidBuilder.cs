using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

public class AndroidBuilder
{
    private const string APK_NAME = "TsukiBRDemo.apk";
    private const string OBB_NAME = "main.1.com.tsuki.battleroyale.obb";

    [MenuItem("Tsuki BR/Build Android APK + OBB")]
    public static void BuildAPKWithOBBMenuItem()
    {
        string buildPath = Path.Combine(Application.dataPath, "..", "build");
        BuildAPKWithOBB(buildPath);
    }

    public static void BuildAPKWithOBB()
    {
        // Pour les scripts batch - récupère le chemin depuis les arguments
        string[] args = System.Environment.GetCommandLineArgs();
        string buildPath = "";
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-buildPath" && i + 1 < args.Length)
            {
                buildPath = args[i + 1];
                break;
            }
        }

        if (string.IsNullOrEmpty(buildPath))
        {
            buildPath = Path.Combine(Application.dataPath, "..", "build");
        }

        BuildAPKWithOBB(buildPath);
    }

    public static void BuildAPKWithOBB(string buildPath)
    {
        Debug.Log("=== Début du build Android APK + OBB ===");

        // Créer le dossier de build
        Directory.CreateDirectory(buildPath);

        // Configuration du build
        ConfigureAndroidSettings();
        
        // Préparer les assets pour l'OBB
        PrepareOBBAssets();

        // Build de l'APK avec OBB
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenesToBuild(),
            locationPathName = Path.Combine(buildPath, APK_NAME),
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        // Activer la génération d'OBB
        PlayerSettings.Android.useAPKExpansionFiles = true;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build réussi!");
            Debug.Log($"APK: {Path.Combine(buildPath, APK_NAME)}");
            
            // L'OBB est généré automatiquement par Unity
            string obbPath = Path.Combine(buildPath, OBB_NAME);
            if (File.Exists(obbPath))
            {
                Debug.Log($"OBB: {obbPath}");
            }
            else
            {
                Debug.LogWarning("Fichier OBB non trouvé après le build");
            }

            // Nettoyage post-build
            CleanupAfterBuild();
        }
        else
        {
            Debug.LogError($"Build échoué: {report.summary.result}");
            
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    Debug.LogError($"Build Error: {message.content}");
                }
            }
        }

        Debug.Log("=== Fin du build Android ===");
    }

    public static void BuildOBBOnly()
    {
        Debug.Log("=== Build OBB seulement ===");
        
        string[] args = System.Environment.GetCommandLineArgs();
        string buildPath = "";
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-buildPath" && i + 1 < args.Length)
            {
                buildPath = args[i + 1];
                break;
            }
        }

        if (string.IsNullOrEmpty(buildPath))
        {
            buildPath = Path.Combine(Application.dataPath, "..", "build");
        }

        BuildOBBOnly(buildPath);
    }

    public static void BuildOBBOnly(string buildPath)
    {
        // Préparer les assets pour l'OBB
        PrepareOBBAssets();

        // Configuration temporaire pour build OBB
        PlayerSettings.Android.useAPKExpansionFiles = true;

        // Build temporaire pour générer l'OBB
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenesToBuild(),
            locationPathName = Path.Combine(buildPath, "temp_" + APK_NAME),
            target = BuildTarget.Android,
            options = BuildOptions.BuildScriptsOnly // Build rapide pour OBB
        };

        BuildPipeline.BuildPlayer(buildPlayerOptions);

        // Nettoyer l'APK temporaire
        string tempAPK = Path.Combine(buildPath, "temp_" + APK_NAME);
        if (File.Exists(tempAPK))
        {
            File.Delete(tempAPK);
        }

        CleanupAfterBuild();
        Debug.Log("OBB rebuild terminé");
    }

    private static void ConfigureAndroidSettings()
    {
        Debug.Log("Configuration des paramètres Android...");

        // Configuration de base
        PlayerSettings.companyName = "Tsuki Games";
        PlayerSettings.productName = "Tsuki BR Demo";
        PlayerSettings.applicationIdentifier = "com.tsuki.battleroyale";

        // Configuration Android spécifique
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.bundleVersion = "1.0";

        // Orientation forcée paysage
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        // Configuration build
        PlayerSettings.Android.useAPKExpansionFiles = true;
        EditorUserBuildSettings.buildAppBundle = false; // APK, pas AAB
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        // Architecture
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        Debug.Log("Paramètres Android configurés");
    }

    private static void PrepareOBBAssets()
    {
        Debug.Log("Préparation des assets pour l'OBB...");

        string streamingAssetsPath = Application.streamingAssetsPath;
        string obbFolder = Path.Combine(streamingAssetsPath, "obb");

        // Créer le dossier OBB s'il n'existe pas
        Directory.CreateDirectory(obbFolder);

        // Copier les assets lourds vers StreamingAssets/obb/
        CopyOBBAssets(obbFolder);

        // Actualiser l'Asset Database
        AssetDatabase.Refresh();
        
        Debug.Log($"Assets préparés dans: {obbFolder}");
    }

    private static void CopyOBBAssets(string obbFolder)
    {
        // Copier les textures HD
        CopyDirectoryIfExists("Assets/Art/Textures", Path.Combine(obbFolder, "textures"));
        
        // Copier les modèles 3D
        CopyDirectoryIfExists("Assets/Art/Models", Path.Combine(obbFolder, "models"));
        
        // Copier les fichiers audio
        CopyDirectoryIfExists("Assets/Audio", Path.Combine(obbFolder, "audio"));

        // Créer des fichiers placeholder si les dossiers n'existent pas
        CreatePlaceholderAssets(obbFolder);
    }

    private static void CopyDirectoryIfExists(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(sourcePath))
        {
            Directory.CreateDirectory(destinationPath);
            
            foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourcePath, file);
                string destFile = Path.Combine(destinationPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                
                if (!File.Exists(destFile) || File.GetLastWriteTime(file) > File.GetLastWriteTime(destFile))
                {
                    File.Copy(file, destFile, true);
                }
            }
        }
    }

    private static void CreatePlaceholderAssets(string obbFolder)
    {
        // Créer des assets placeholder pour la demo
        string texturesFolder = Path.Combine(obbFolder, "textures");
        string modelsFolder = Path.Combine(obbFolder, "models");
        string audioFolder = Path.Combine(obbFolder, "audio");

        Directory.CreateDirectory(texturesFolder);
        Directory.CreateDirectory(modelsFolder);
        Directory.CreateDirectory(audioFolder);

        // Créer des fichiers placeholder
        CreatePlaceholderFile(Path.Combine(texturesFolder, "player_texture.txt"), "Placeholder pour texture joueur");
        CreatePlaceholderFile(Path.Combine(texturesFolder, "weapon_textures.txt"), "Placeholder pour textures d'armes");
        CreatePlaceholderFile(Path.Combine(modelsFolder, "player_model.txt"), "Placeholder pour modèle joueur");
        CreatePlaceholderFile(Path.Combine(modelsFolder, "weapon_models.txt"), "Placeholder pour modèles d'armes");
        CreatePlaceholderFile(Path.Combine(audioFolder, "game_music.txt"), "Placeholder pour musique de jeu");
        CreatePlaceholderFile(Path.Combine(audioFolder, "sound_effects.txt"), "Placeholder pour effets sonores");
    }

    private static void CreatePlaceholderFile(string filePath, string content)
    {
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, $"{content}\nCe fichier sera remplacé par les vrais assets dans la version finale.");
        }
    }

    private static string[] GetScenesToBuild()
    {
        // Récupérer les scènes à inclure dans le build
        string[] scenePaths = new string[EditorBuildSettings.scenes.Length];
        
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            scenePaths[i] = EditorBuildSettings.scenes[i].path;
        }

        // Si aucune scène configurée, utiliser Main.unity
        if (scenePaths.Length == 0)
        {
            scenePaths = new string[] { "Assets/Scenes/Main.unity" };
        }

        return scenePaths;
    }

    private static void CleanupAfterBuild()
    {
        Debug.Log("Nettoyage post-build...");
        
        // Actualiser la base de données des assets
        AssetDatabase.Refresh();
        
        // Autres nettoyages si nécessaire
        Debug.Log("Nettoyage terminé");
    }

    [MenuItem("Tsuki BR/Configure Android Settings")]
    public static void ConfigureAndroidSettingsMenuItem()
    {
        ConfigureAndroidSettings();
        Debug.Log("Paramètres Android configurés via menu");
    }

    [MenuItem("Tsuki BR/Prepare OBB Assets")]
    public static void PrepareOBBAssetsMenuItem()
    {
        PrepareOBBAssets();
        Debug.Log("Assets OBB préparés via menu");
    }

    [MenuItem("Tsuki BR/Open Build Folder")]
    public static void OpenBuildFolder()
    {
        string buildPath = Path.Combine(Application.dataPath, "..", "build");
        if (Directory.Exists(buildPath))
        {
            EditorUtility.RevealInFinder(buildPath);
        }
        else
        {
            Debug.LogWarning("Dossier build non trouvé. Lancez d'abord un build.");
        }
    }
}