using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Removes the superseded game art and imported examples only after the saved gameplay
/// scene, its asset dependency closure and the runtime Resources roots are checked.
/// </summary>
public static class SealAssetCleanup
{
    private const string ScenePath = "Assets/Scenes/GamePlay.unity";
    private const string BackgroundPath = "Assets/Resources/Characters/ArcticBand/background.png";
    private const string OriginalAtlas = "Assets/Resources/Art/Level_cellanim/Sprite/cellanim.tpl.png";
    private const string ReportPath = "Logs/SealValidation/Optimization/cleanup.txt";
    private static readonly string[] RuntimeRoots =
    {
        "Assets/Resources/Art/Level_cellanim/Kore/haibao.asset",
        "Assets/Resources/Art/Level_cellanim/OriginalGoma/goma.json",
        OriginalAtlas,
        "Assets/Resources/Characters/ArcticBand/seals.png",
        "Assets/Resources/Characters/ArcticBand/captain.png",
        BackgroundPath,
        "Assets/Resources/UI/SealPaper/seal-ui-atlas.png"
    };

    [MenuItem("Tools/Seal Rhythm/Clean Unused Game Assets")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Asset cleanup must run outside Play mode.");

        var report = new StringBuilder();
        report.AppendLine("Seal Rhythm asset cleanup");
        report.AppendLine("Time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine("Scene=" + ScenePath);
        try
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var games = ComponentsIn<RhythmGame>(scene);
            Require(games.Length == 1, "Expected exactly one RhythmGame in GamePlay.");
            var game = games[0];
            var player = game.characterAnimator;
            Require(player != null, "The player Animator reference must already be assigned.");

            var partners = ComponentsIn<xuRenOtherKoreo>(scene);
            var people = ComponentsIn<PeopleController>(scene);
            var seals = new HashSet<Animator> { player };
            foreach (var partner in partners)
            {
                var animator = partner.GetComponent<Animator>();
                Require(animator != null, "A partner seal is missing its root Animator.");
                seals.Add(animator);
            }
            Require(seals.Count == 4 && partners.Length == 3 && people.Length == 1,
                "Expected four seal actors and one conductor; refusing to modify an unfamiliar scene.");
            var captain = people[0].GetComponent<Animator>();
            Require(captain != null, "The conductor is missing its root Animator.");
            var background = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .SingleOrDefault(renderer => renderer.gameObject.name == "Bg");
            Require(background != null, "GamePlay must contain one Bg SpriteRenderer.");
            var replacement = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            Require(replacement != null, "Import the new background as Sprite (Single) before cleanup.");

            // Preflight every actor before unpacking or deleting any hierarchy.
            foreach (var actor in seals.Concat(new[] { captain })) ValidateActor(actor);
            foreach (var actor in seals) TrimActor(actor, report);
            TrimActor(captain, report);
            Require(game.characterAnimator == player, "Cleanup changed the player Animator object.");

            game.missTextPrefab = null;
            game.perfectTextPrefab = null;
            game.scoreText = null;
            game.uiCanvasTransform = null;
            EditorUtility.SetDirty(game);
            RemoveLegacyCanvases(scene, report);
            ReplaceBackground(background, replacement);
            report.AppendLine("Scene: Bg uses the new background with its original world bounds preserved.");

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save the cleaned GamePlay scene.");
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var candidates = Candidates().Where(Exists).Distinct(StringComparer.Ordinal).ToArray();
            var roots = new[] { ScenePath }.Concat(RuntimeRoots).ToArray();
            foreach (var runtimeRoot in RuntimeRoots)
                Require(Exists(runtimeRoot), "Required runtime resource is missing: " + runtimeRoot);
            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies(roots, true), StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                ValidateWorkspaceAssetPath(candidate);
                var retained = dependencies.Where(path => IsWithin(path, candidate)).ToArray();
                Require(retained.Length == 0,
                    "Candidate is still required by GamePlay/runtime resources; nothing has been deleted: " +
                    candidate + "\n" + string.Join("\n", retained));
            }

            long assetBytes = 0, metaBytes = 0;
            int assetCount = 0;
            report.AppendLine("Dependency check=PASS (saved GamePlay and all explicit runtime Resources roots)");
            report.AppendLine("Deletion candidates=" + candidates.Length);
            foreach (var candidate in candidates)
            {
                report.AppendLine("DELETE " + candidate);
                foreach (var file in SourceFiles(candidate))
                {
                    long bytes = new FileInfo(file).Length;
                    bool isMeta = file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
                    if (isMeta) metaBytes += bytes;
                    else { assetBytes += bytes; ++assetCount; }
                    report.AppendLine("  " + RelativePath(file) + " | " + bytes + " bytes");
                }
            }
            report.AppendLine("SourceAssets=" + assetCount);
            report.AppendLine("SourceAssetBytes=" + assetBytes);
            report.AppendLine("SourceMetaBytes=" + metaBytes);
            report.AppendLine("TotalSourceBytes=" + (assetBytes + metaBytes));
            report.AppendLine("Status=dependency checks passed; deletion starting");
            SaveReport(report);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var candidate in candidates)
                    Require(AssetDatabase.DeleteAsset(candidate), "AssetDatabase could not delete " + candidate);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();

            Require(game.characterAnimator == player && player != null, "Player Animator was not preserved.");
            var remainingDependencies = AssetDatabase.GetDependencies(roots, true);
            foreach (var candidate in candidates)
                Require(!remainingDependencies.Any(path => IsWithin(path, candidate)), "Deleted path remains in runtime dependency graph: " + candidate);
            report.AppendLine("Status=COMPLETE");
            SaveReport(report);
            Debug.Log("Seal asset cleanup complete: " + assetCount + " unused source assets, " +
                (assetBytes + metaBytes) + " source bytes removed. Report: " + ReportPath);
        }
        catch (Exception exception)
        {
            report.AppendLine("Status=FAILED");
            report.AppendLine(exception.ToString());
            SaveReport(report);
            throw;
        }
    }

    private static T[] ComponentsIn<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static void ValidateActor(Animator actor)
    {
        Require(actor != null && actor.gameObject.scene.path == ScenePath, "Actor is outside GamePlay.");
        Require(actor.GetComponent<RhythmGame>() == null && actor.GetComponent<AudioSource>() == null,
            "An actor also contains game or audio logic; refusing hierarchy cleanup.");
        foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Require(behaviour != null, "An actor contains a missing script.");
            Require(behaviour is PeopleController || behaviour is xuRenOtherKoreo,
                "An actor contains unexpected logic: " + behaviour.GetType().FullName);
            Require(behaviour.transform == actor.transform, "Actor logic is attached to a visual child.");
        }
    }

    private static void TrimActor(Animator actor, StringBuilder report)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(actor.gameObject))
        {
            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(actor.gameObject);
            Require(prefabRoot == actor.gameObject, "Actor is nested in an unexpected prefab.");
            PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        int children = actor.transform.childCount;
        while (actor.transform.childCount > 0) Object.DestroyImmediate(actor.transform.GetChild(0).gameObject);
        actor.runtimeAnimatorController = null;
        // Current artwork is attached at runtime. Keep the same root Animator object as the
        // gameplay binding and the original controller markers used to discover the team.
        foreach (var component in actor.GetComponents<Component>())
            if (!(component is Transform) && !(component is Animator) &&
                !(component is PeopleController) && !(component is xuRenOtherKoreo)) Object.DestroyImmediate(component);
        EditorUtility.SetDirty(actor);
        report.AppendLine("Scene: kept actor root/Animator " + actor.name + "; removed " + children + " legacy visual child roots and its AnimatorController.");
    }

    private static void RemoveLegacyCanvases(Scene scene, StringBuilder report)
    {
        foreach (var canvas in ComponentsIn<Canvas>(scene))
        {
            if (canvas == null || canvas.gameObject.name != "Canvas" || canvas.transform.parent != null) continue;
            bool onlyLegacyUi = canvas.GetComponentsInChildren<Component>(true).All(component =>
                component != null && (component is RectTransform || component is Canvas || component is CanvasRenderer ||
                    component is Text || component is CanvasScaler || component is GraphicRaycaster));
            if (!onlyLegacyUi)
            {
                report.AppendLine("Scene: retained Canvas because it contains other components.");
                continue;
            }
            Object.DestroyImmediate(canvas.gameObject);
            report.AppendLine("Scene: removed the legacy score Canvas (UI-only hierarchy).");
        }
    }

    private static void ReplaceBackground(SpriteRenderer renderer, Sprite replacement)
    {
        var previous = renderer.bounds;
        Require(previous.size.x > 0 && previous.size.y > 0, "The old stage background has empty bounds.");
        renderer.sprite = replacement;
        renderer.color = Color.white;
        var currentSize = renderer.bounds.size;
        Require(currentSize.x > 0 && currentSize.y > 0, "The replacement background has empty bounds.");
        renderer.transform.localScale = Vector3.Scale(renderer.transform.localScale,
            new Vector3(previous.size.x / currentSize.x, previous.size.y / currentSize.y, 1));
        renderer.transform.position += previous.center - renderer.bounds.center;
        var updated = renderer.bounds;
        Require((updated.center - previous.center).sqrMagnitude < .000001f &&
            (updated.size - previous.size).sqrMagnitude < .000001f, "Replacing the background changed stage bounds.");
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(renderer.transform);
    }

    private static IEnumerable<string> Candidates()
    {
        yield return "Assets/Resources/Art/Level_cellanim/Animation";
        yield return "Assets/Resources/Art/Level_cellanim/Characters";
        yield return "Assets/Resources/Art/Level_cellanim/UI";
        const string spriteDirectory = "Assets/Resources/Art/Level_cellanim/Sprite";
        foreach (var file in Directory.GetFiles(spriteDirectory).OrderBy(path => path, StringComparer.Ordinal))
        {
            var path = file.Replace('\\', '/');
            if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && path != OriginalAtlas) yield return path;
        }
        foreach (var script in new[] { "AniController", "GameController", "KoreController", "noteObject", "playerController" })
            yield return "Assets/Scripts/" + script + ".cs";
        yield return "Assets/Koreographer/Demos";
        yield return "Assets/Koreographer/Integrations";
        yield return "Assets/Koreographer/Documentation";
        yield return "Assets/Koreographer/Credit Textures";
        yield return "Assets/Kore";
        yield return "Assets/LevelGame";
        yield return "Assets/2D Pixel Art Platformer Biome - American Forest";
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static bool IsWithin(string path, string candidate) => path == candidate || path.StartsWith(candidate + "/", StringComparison.Ordinal);

    private static void ValidateWorkspaceAssetPath(string assetPath)
    {
        string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string absolute = Path.GetFullPath(assetPath);
        Require(assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
            absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase), "Deletion path escapes project Assets: " + assetPath);
    }

    private static IEnumerable<string> SourceFiles(string path)
    {
        if (Directory.Exists(path))
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal)) yield return file;
        else if (File.Exists(path)) yield return path;
        if (File.Exists(path + ".meta")) yield return path + ".meta";
    }

    private static string RelativePath(string path)
    {
        string absolute = Path.GetFullPath(path);
        string root = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? absolute.Substring(root.Length).Replace('\\', '/') : path;
    }

    private static void SaveReport(StringBuilder report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
