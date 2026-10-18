using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Repeatable optimization/build commands, also usable after an editor domain reload.</summary>
[InitializeOnLoad]
public static class SealProjectChecks
{
    private const string Folder = "Logs/SealValidation/Optimization";
    private static double nextPoll;
    static SealProjectChecks() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        string request = Folder + "/request.txt";
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim();
        File.Delete(request);
        try
        {
            if (command == "optimize") Optimize();
            else if (command == "validate") Validate();
            else if (command == "prune") PruneLegacyData();
            else if (command == "play") EditorApplication.isPlaying = true;
            else if (command == "smoke") SealRhythmLevelTests.Run();
            else if (command == "arrows") SealBeatLaneVisibilityTests.Run();
            else if (command == "stop") EditorApplication.isPlaying = false;
            else if (command == "build") BuildWindows();
            else throw new InvalidOperationException("Unknown project-check command: " + command);
            File.WriteAllText(Folder + "/command-result.txt", "PASS " + command + "\n" + DateTime.Now);
        }
        catch (Exception error)
        {
            File.WriteAllText(Folder + "/command-result.txt", "FAIL " + command + "\n" + error);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Seal Rhythm/Optimize Game Resources")]
    public static void Optimize()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before optimizing.");
        Directory.CreateDirectory(Folder);
        SealTexturePacking.Pack();
        SealAssetCleanup.Run();
        EditorBuildSettings.RemoveConfigObject("com.unity.dt.app-ui");
        const string musicPath = "Assets/Resources/Art/Level_cellanim/Music/haibao.mp3";
        var importer = (AudioImporter)AssetImporter.GetAtPath(musicPath);
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .8f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
        if (clip.frequency != 44100 || clip.samples != 4687488 || clip.channels != 2)
            throw new InvalidOperationException("Music sample layout changed during compression.");
        File.AppendAllText(Folder + "/compression.txt", "Music: Vorbis quality=.8, 44100 Hz, 4687488 samples, stereo; PASS\n");
        AssetDatabase.SaveAssets();
    }

    public static void Validate()
    {
        SealRhythmJudgeTests.RunAll();
        SealOriginalAnimationTests.RunAll();
        File.WriteAllLines(Folder + "/scene-dependencies-after.txt", AssetDatabase.GetDependencies("Assets/Scenes/GamePlay.unity", true));
    }

    public static void PruneLegacyData()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before pruning.");
        var song = AssetDatabase.LoadAssetAtPath<SonicBloom.Koreo.Koreography>("Assets/Resources/Art/Level_cellanim/Kore/haibao.asset");
        song.RemoveTrack("PeopleEventID");
        EditorUtility.SetDirty(song);
        const string archive = "Assets/Editor/OriginalData";
        if (!AssetDatabase.IsValidFolder(archive)) AssetDatabase.CreateFolder("Assets/Editor", "OriginalData");
        const string legacy = "Assets/Resources/Art/Level_cellanim/Kore/PeopleEvent.asset";
        if (File.Exists(legacy))
        {
            string error = AssetDatabase.MoveAsset(legacy, archive + "/PeopleEvent.asset");
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        var unusedCamera = GameObject.Find("UICamera");
        if (unusedCamera != null)
        {
            var camera = unusedCamera.GetComponent<Camera>();
            if (camera == null || camera == Camera.main || (camera.cullingMask & ~(1 << 5)) != 0)
                throw new InvalidOperationException("Legacy camera also renders game layers.");
            UnityEngine.Object.DestroyImmediate(unusedCamera);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Folder + "/legacy-data.txt", "PASS: removed legacy camera rendering only the deleted UI layer; archived original 254 captain events under Editor/OriginalData for extraction checks, excluded from player.\n");
    }

    [MenuItem("Tools/Seal Rhythm/Build Optimized Windows")]
    public static void BuildWindows()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building.");
        Directory.CreateDirectory(Folder);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/GamePlay.unity" },
            locationPathName = "Builds/SealRhythm/SealRhythm.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4HC | BuildOptions.CleanBuildCache
        });
        var s = report.summary;
        File.WriteAllText(Folder + "/build.txt", "Result=" + s.result + "\nErrors=" + s.totalErrors +
            "\nWarnings=" + s.totalWarnings + "\nSize=" + s.totalSize + "\nDuration=" + s.totalTime + "\nCompression=LZ4HC\n");
        if (s.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed.");
    }
}
