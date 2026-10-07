using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;


namespace Virtuademy.SDK.Environments.VisualScripting.Editor
{
  /// <summary>
  /// Project-wide update routine for <see cref="CheckPlatformUnit"/> ("Virtuademy Platform: Switch").
  /// Scans every graph asset, prefab and scene under Assets/ for occurrences of the node,
  /// lists them in a window (toggle to select, button to focus the asset/graph), and lets the
  /// user copy the WebGL control connection onto the newly added Mobile port for all
  /// selected occurrences.
  /// </summary>
  public class CheckPlatformUnitMobilePortUpdater : EditorWindow
  {
    #region Types

    private enum ContainerKind
    {
      GraphAsset,
      Prefab,
      Scene,
    }

    private enum MobileStatus
    {
      NotConnected, // WebGL is connected, Mobile is not — the case this routine fixes.
      Aligned,      // Mobile has the same destination as WebGL (or both are unconnected).
      Custom,       // Mobile deliberately points to a different destination — nothing to fix.
    }

    private class Occurrence
    {
      public ContainerKind Kind;
      public string AssetPath;

      // Scene/prefab only: where the machine component lives.
      public string GameObjectPath;
      public List<int> IndexPath = new();
      public int MachineIndex;

      // Where the unit lives inside the root graph (chain of embedded nester element guids).
      public List<Guid> NestingPath = new();
      public string GraphLabel;
      public Guid UnitGuid;
      public Vector2 UnitPosition;

      public string WebGLSummary;
      public string MobileSummary;
      public MobileStatus Status;
      public string Error;

      public bool Selected;
    }

    #endregion

    private const string NodeTypeName = nameof(CheckPlatformUnit);
    private const string WindowTitle = "Platform Switch: Mobile port updater";

    private readonly List<Occurrence> occurrences = new();
    private Vector2 scrollPosition;
    private bool hasScanned;
    private bool scenesSkipped;

    [MenuItem("Virtuademy/Update routines/v2026.4.x -> v2026.5.0")]
    public static void Open()
    {
      CheckPlatformUnitMobilePortUpdater window = GetWindow<CheckPlatformUnitMobilePortUpdater>(false, WindowTitle, true);
      window.minSize = new Vector2(560, 320);
      window.Show();
      window.ScanProject();
    }

    #region GUI

    private void OnGUI()
    {
      DrawToolbar();

      if (!hasScanned)
      {
        EditorGUILayout.HelpBox("Press \"Rescan project\" to search for Platform Switch nodes.", MessageType.Info);
        return;
      }

      if (scenesSkipped)
      {
        EditorGUILayout.HelpBox("Scene scan was skipped (unsaved scene changes). Save your scenes and rescan to include scenes.", MessageType.Warning);
      }

      if (occurrences.Count == 0)
      {
        EditorGUILayout.HelpBox("No Platform Switch node found under Assets/.", MessageType.Info);
        return;
      }

      EditorGUILayout.Space(4);
      EditorGUILayout.LabelField(
          $"{occurrences.Count} occurrence(s) — " +
          $"{Count(ContainerKind.GraphAsset)} in graph assets, " +
          $"{Count(ContainerKind.Prefab)} in prefabs, " +
          $"{Count(ContainerKind.Scene)} in scenes.",
          EditorStyles.miniLabel);

      scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

      DrawGroup("Graph assets", ContainerKind.GraphAsset);
      DrawGroup("Prefabs (embedded graphs)", ContainerKind.Prefab);
      DrawGroup("Scenes (embedded graphs)", ContainerKind.Scene);

      EditorGUILayout.EndScrollView();

      DrawFooter();
    }

    private void DrawToolbar()
    {
      using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
      {
        if (GUILayout.Button("Rescan project", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
          ScanProject();
        }

        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(occurrences.Count == 0))
        {
          if (GUILayout.Button("Select all", EditorStyles.toolbarButton, GUILayout.Width(70)))
          {
            occurrences.ForEach(o => o.Selected = true);
          }
          if (GUILayout.Button("Select none", EditorStyles.toolbarButton, GUILayout.Width(80)))
          {
            occurrences.ForEach(o => o.Selected = false);
          }
          if (GUILayout.Button("Select unconnected", EditorStyles.toolbarButton, GUILayout.Width(120)))
          {
            occurrences.ForEach(o => o.Selected = o.Status == MobileStatus.NotConnected);
          }
        }
      }
    }

    private void DrawGroup(string header, ContainerKind kind)
    {
      List<Occurrence> group = occurrences.Where(o => o.Kind == kind).ToList();
      if (group.Count == 0)
      {
        return;
      }

      EditorGUILayout.Space(6);
      EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

      foreach (Occurrence occ in group)
      {
        DrawRow(occ);
      }
    }

    private void DrawRow(Occurrence occ)
    {
      using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
      {
        occ.Selected = EditorGUILayout.Toggle(occ.Selected, GUILayout.Width(18));

        using (new EditorGUILayout.VerticalScope())
        {
          EditorGUILayout.LabelField(occ.AssetPath, EditorStyles.boldLabel);

          string detail = occ.Kind == ContainerKind.GraphAsset
              ? occ.GraphLabel
              : $"{occ.GameObjectPath}  ·  machine #{occ.MachineIndex}  ·  {occ.GraphLabel}";
          EditorGUILayout.LabelField(detail, EditorStyles.miniLabel);

          if (!string.IsNullOrEmpty(occ.Error))
          {
            EditorGUILayout.LabelField(occ.Error, ErrorStyle);
          }
          else
          {
            (GUIStyle style, string verdict) = occ.Status switch
            {
              MobileStatus.Aligned => (AlignedStyle, "aligned with WebGL"),
              MobileStatus.Custom => (CustomStyle, "custom Mobile connection"),
              _ => (NotConnectedStyle, "Mobile not connected"),
            };

            EditorGUILayout.LabelField(
                $"WebGL → {occ.WebGLSummary}     Mobile → {occ.MobileSummary}     ·  {verdict}",
                style);
          }
        }

        if (GUILayout.Button("Focus", GUILayout.Width(56), GUILayout.Height(22)))
        {
          FocusOccurrence(occ);
        }
      }
    }

    private void DrawFooter()
    {
      int selectedCount = occurrences.Count(o => o.Selected);

      EditorGUILayout.Space(4);
      EditorGUILayout.HelpBox(
          "For every selected node, the Mobile output will be connected to the same destination " +
          "as the WebGL output (an existing Mobile connection is replaced; if WebGL is not " +
          "connected, Mobile is disconnected too).",
          MessageType.None);

      using (new EditorGUI.DisabledScope(selectedCount == 0))
      {
        if (GUILayout.Button($"Align Mobile connections with WebGL ({selectedCount} selected)", GUILayout.Height(30)))
        {
          ApplySelected();
        }
      }
      EditorGUILayout.Space(4);
    }

    private int Count(ContainerKind kind) => occurrences.Count(o => o.Kind == kind);

    private static GUIStyle alignedStyle;
    private static GUIStyle AlignedStyle => alignedStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
      normal = { textColor = new Color(0.3f, 0.75f, 0.3f) },
    };

    private static GUIStyle customStyle;
    private static GUIStyle CustomStyle => customStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
      normal = { textColor = new Color(0.35f, 0.7f, 0.75f) },
    };

    private static GUIStyle notConnectedStyle;
    private static GUIStyle NotConnectedStyle => notConnectedStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
      normal = { textColor = new Color(0.95f, 0.65f, 0.2f) },
    };

    private static GUIStyle errorStyle;
    private static GUIStyle ErrorStyle => errorStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
      normal = { textColor = new Color(0.9f, 0.35f, 0.3f) },
    };

    #endregion

    #region Scan

    private void ScanProject()
    {
      occurrences.Clear();
      scenesSkipped = false;

      try
      {
        ScanGraphAssets();
        ScanPrefabs();
        ScanScenes();
      }
      finally
      {
        EditorUtility.ClearProgressBar();
      }

      hasScanned = true;

      // Preselect only the rows that actually need fixing: WebGL connected, Mobile not.
      occurrences.ForEach(o => o.Selected = o.Status == MobileStatus.NotConnected);

      Repaint();
    }

    private void ScanGraphAssets()
    {
      string[] guids = AssetDatabase.FindAssets("t:ScriptGraphAsset", new[] { "Assets" })
          .Concat(AssetDatabase.FindAssets("t:StateGraphAsset", new[] { "Assets" }))
          .Distinct()
          .ToArray();

      for (int i = 0; i < guids.Length; i++)
      {
        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
        EditorUtility.DisplayProgressBar(WindowTitle, $"Scanning graph assets… {path}", (float)i / guids.Length);

        if (!FileMightContainNode(path))
        {
          continue;
        }

        if (AssetDatabase.LoadAssetAtPath<UnityObject>(path) is not IMacro macro || macro.graph == null)
        {
          continue;
        }

        foreach ((List<Guid> nestingPath, string graphLabel, CheckPlatformUnit unit) in CollectUnits(macro.graph))
        {
          AddOccurrence(ContainerKind.GraphAsset, path, null, null, 0, nestingPath, graphLabel, unit);
        }
      }
    }

    private void ScanPrefabs()
    {
      string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

      for (int i = 0; i < guids.Length; i++)
      {
        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
        EditorUtility.DisplayProgressBar(WindowTitle, $"Scanning prefabs… {path}", (float)i / guids.Length);

        if (!FileMightContainNode(path))
        {
          continue;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
          continue;
        }

        ScanHierarchy(prefab.transform, prefab.transform, ContainerKind.Prefab, path);
      }
    }

    private void ScanScenes()
    {
      string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

      List<string> candidates = new();
      for (int i = 0; i < guids.Length; i++)
      {
        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
        if (FileMightContainNode(path))
        {
          candidates.Add(path);
        }
      }

      if (candidates.Count == 0)
      {
        return;
      }

      if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      {
        scenesSkipped = true;
        return;
      }

      SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

      try
      {
        for (int i = 0; i < candidates.Count; i++)
        {
          string path = candidates[i];
          EditorUtility.DisplayProgressBar(WindowTitle, $"Scanning scenes… {path}", (float)i / candidates.Count);

          Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
          foreach (GameObject root in scene.GetRootGameObjects())
          {
            ScanHierarchy(root.transform, null, ContainerKind.Scene, path);
          }
        }
      }
      finally
      {
        RestoreSceneSetup(setup);
      }
    }

    private void ScanHierarchy(Transform hierarchyRoot, Transform indexPathRoot, ContainerKind kind, string assetPath)
    {
      foreach (MonoBehaviour behaviour in hierarchyRoot.GetComponentsInChildren<MonoBehaviour>(true))
      {
        if (behaviour == null || behaviour is not IMachine || behaviour is not IGraphNester nester)
        {
          continue;
        }

        // Machines pointing to a macro asset are covered by the graph-asset scan.
        if (nester.nest == null || nester.nest.source != GraphSource.Embed || nester.nest.embed == null)
        {
          continue;
        }

        int machineIndex = MachinesOn(behaviour.transform).IndexOf(behaviour);

        foreach ((List<Guid> nestingPath, string graphLabel, CheckPlatformUnit unit) in CollectUnits(nester.nest.embed))
        {
          AddOccurrence(kind, assetPath, behaviour.transform, indexPathRoot, machineIndex, nestingPath, graphLabel, unit);
        }
      }
    }

    private void AddOccurrence(ContainerKind kind, string assetPath, Transform owner, Transform indexPathRoot,
        int machineIndex, List<Guid> nestingPath, string graphLabel, CheckPlatformUnit unit)
    {
      Occurrence occ = new()
      {
        Kind = kind,
        AssetPath = assetPath,
        GameObjectPath = owner == null ? null : GetTransformPath(owner),
        IndexPath = owner == null ? new List<int>() : GetIndexPath(owner, indexPathRoot),
        MachineIndex = machineIndex,
        NestingPath = nestingPath,
        GraphLabel = graphLabel,
        UnitGuid = unit.guid,
        UnitPosition = unit.position,
      };

      RefreshStatus(occ, unit);
      occurrences.Add(occ);
    }

    /// <summary>
    /// Cheap pre-filter: text-serialized assets that never mention the node type name
    /// cannot contain the unit, so they are skipped without deserializing anything.
    /// Binary or unreadable files conservatively pass the filter.
    /// </summary>
    private static bool FileMightContainNode(string assetPath)
    {
      try
      {
        using StreamReader reader = new(Path.GetFullPath(assetPath));

        string firstLine = reader.ReadLine();
        if (firstLine == null || !firstLine.StartsWith("%YAML"))
        {
          return true;
        }

        string line;
        while ((line = reader.ReadLine()) != null)
        {
          if (line.Contains(NodeTypeName))
          {
            return true;
          }
        }

        return false;
      }
      catch
      {
        return true;
      }
    }

    #endregion

    #region Graph traversal

    private static IEnumerable<(List<Guid> nestingPath, string graphLabel, CheckPlatformUnit unit)> CollectUnits(IGraph rootGraph)
    {
      List<(List<Guid>, string, CheckPlatformUnit)> results = new();
      CollectUnitsRecursive(rootGraph, new List<Guid>(), "Graph", results);
      return results;
    }

    private static void CollectUnitsRecursive(IGraph graph, List<Guid> nestingPath, string graphLabel,
        List<(List<Guid>, string, CheckPlatformUnit)> results)
    {
      if (graph == null)
      {
        return;
      }

      foreach (IGraphElement element in graph.elements)
      {
        if (element is CheckPlatformUnit unit)
        {
          results.Add((new List<Guid>(nestingPath), graphLabel, unit));
        }

        // Embedded subgraphs, states and state transitions all implement IGraphNesterElement.
        if (element is IGraphNesterElement nesterElement && nesterElement.nest != null
            && nesterElement.nest.source == GraphSource.Embed && nesterElement.nest.embed != null)
        {
          nestingPath.Add(element.guid);
          CollectUnitsRecursive(nesterElement.nest.embed, nestingPath, $"{graphLabel} ▸ {NesterLabel(nesterElement)}", results);
          nestingPath.RemoveAt(nestingPath.Count - 1);
        }
      }
    }

    private static IGraph ResolveNestedGraph(IGraph rootGraph, List<Guid> nestingPath)
    {
      IGraph graph = rootGraph;

      foreach (Guid guid in nestingPath)
      {
        IGraphNesterElement nester = graph?.elements
            .OfType<IGraphNesterElement>()
            .FirstOrDefault(e => e.guid == guid);

        graph = nester?.nest?.embed;
        if (graph == null)
        {
          return null;
        }
      }

      return graph;
    }

    private static CheckPlatformUnit ResolveUnit(IGraph rootGraph, Occurrence occ)
    {
      IGraph graph = ResolveNestedGraph(rootGraph, occ.NestingPath);
      return graph?.elements.OfType<CheckPlatformUnit>().FirstOrDefault(u => u.guid == occ.UnitGuid);
    }

    private static string NesterLabel(IGraphNesterElement nester)
    {
      string title = (nester.nest?.graph as Graph)?.title;
      return string.IsNullOrEmpty(title) ? ObjectNames.NicifyVariableName(nester.GetType().Name) : title;
    }

    #endregion

    #region Status / align

    private static void RefreshStatus(Occurrence occ, CheckPlatformUnit unit)
    {
      unit.EnsureDefined();

      ControlInput webglDestination = unit.OutputTriggerWebGL?.validConnections.FirstOrDefault()?.destination;
      ControlInput mobileDestination = unit.OutputTriggerMobile?.validConnections.FirstOrDefault()?.destination;

      occ.WebGLSummary = Describe(webglDestination);
      occ.MobileSummary = Describe(mobileDestination);
      occ.Status = webglDestination == mobileDestination ? MobileStatus.Aligned
          : mobileDestination == null ? MobileStatus.NotConnected
          : MobileStatus.Custom;
      occ.Error = null;
    }

    /// <summary>
    /// Makes the Mobile output connection identical to the WebGL one.
    /// Returns true when the graph was modified.
    /// </summary>
    private static bool AlignMobileWithWebGL(CheckPlatformUnit unit)
    {
      unit.EnsureDefined();

      ControlOutput webgl = unit.OutputTriggerWebGL;
      ControlOutput mobile = unit.OutputTriggerMobile;

      if (webgl == null || mobile == null)
      {
        throw new InvalidOperationException("Unit ports are not defined — is the node up to date?");
      }

      ControlInput target = webgl.validConnections.FirstOrDefault()?.destination;
      ControlInput current = mobile.validConnections.FirstOrDefault()?.destination;

      if (target == current)
      {
        return false;
      }

      mobile.Disconnect();

      if (target != null)
      {
        mobile.ConnectToValid(target);
      }

      return true;
    }

    private static string Describe(ControlInput destination)
    {
      if (destination == null)
      {
        return "(not connected)";
      }

      return $"{UnitDisplayName(destination.unit)} · {destination.key}";
    }

    private static string UnitDisplayName(IUnit unit)
    {
      if (unit == null)
      {
        return "?";
      }

      Type type = unit.GetType();
      string title = type.GetCustomAttribute<UnitTitleAttribute>()?.title;
      return string.IsNullOrEmpty(title) ? ObjectNames.NicifyVariableName(type.Name) : title;
    }

    #endregion

    #region Apply

    private void ApplySelected()
    {
      List<Occurrence> selected = occurrences.Where(o => o.Selected).ToList();
      if (selected.Count == 0)
      {
        return;
      }

      if (!EditorUtility.DisplayDialog(
              WindowTitle,
              $"Align the Mobile connection with the WebGL one on {selected.Count} node(s)?\n\n" +
              "Existing Mobile connections on the selected nodes will be replaced.",
              "Align", "Cancel"))
      {
        return;
      }

      int changed = 0, untouched = 0, failed = 0;

      try
      {
        ApplyToGraphAssets(selected, ref changed, ref untouched, ref failed);
        ApplyToPrefabs(selected, ref changed, ref untouched, ref failed);
        ApplyToScenes(selected, ref changed, ref untouched, ref failed);

        AssetDatabase.SaveAssets();
      }
      finally
      {
        EditorUtility.ClearProgressBar();
      }

      EditorUtility.DisplayDialog(
          WindowTitle,
          $"Done.\n\nModified: {changed}\nAlready aligned: {untouched}\nFailed: {failed}" +
          (failed > 0 ? "\n\nSee the Console for details." : string.Empty),
          "OK");

      Repaint();
    }

    private void ApplyToGraphAssets(List<Occurrence> selected, ref int changed, ref int untouched, ref int failed)
    {
      foreach (IGrouping<string, Occurrence> byAsset in selected
                   .Where(o => o.Kind == ContainerKind.GraphAsset)
                   .GroupBy(o => o.AssetPath))
      {
        EditorUtility.DisplayProgressBar(WindowTitle, $"Updating {byAsset.Key}…", 0f);

        if (AssetDatabase.LoadAssetAtPath<UnityObject>(byAsset.Key) is not IMacro macro || macro.graph == null)
        {
          failed += Fail(byAsset, $"Could not load graph asset {byAsset.Key}");
          continue;
        }

        bool dirty = false;
        foreach (Occurrence occ in byAsset)
        {
          ProcessOccurrence(occ, macro.graph, ref changed, ref untouched, ref failed, ref dirty);
        }

        if (dirty)
        {
          EditorUtility.SetDirty((UnityObject)macro);
        }
      }
    }

    private void ApplyToPrefabs(List<Occurrence> selected, ref int changed, ref int untouched, ref int failed)
    {
      foreach (IGrouping<string, Occurrence> byPrefab in selected
                   .Where(o => o.Kind == ContainerKind.Prefab)
                   .GroupBy(o => o.AssetPath))
      {
        EditorUtility.DisplayProgressBar(WindowTitle, $"Updating {byPrefab.Key}…", 0f);

        GameObject root = PrefabUtility.LoadPrefabContents(byPrefab.Key);

        try
        {
          bool dirty = false;
          foreach (Occurrence occ in byPrefab)
          {
            IGraph machineGraph = ResolveMachineGraph(ResolveChildPath(root.transform, occ.IndexPath), occ);
            ProcessOccurrence(occ, machineGraph, ref changed, ref untouched, ref failed, ref dirty);
          }

          if (dirty)
          {
            PrefabUtility.SaveAsPrefabAsset(root, byPrefab.Key);
          }
        }
        finally
        {
          PrefabUtility.UnloadPrefabContents(root);
        }
      }
    }

    private void ApplyToScenes(List<Occurrence> selected, ref int changed, ref int untouched, ref int failed)
    {
      List<IGrouping<string, Occurrence>> sceneGroups = selected
          .Where(o => o.Kind == ContainerKind.Scene)
          .GroupBy(o => o.AssetPath)
          .ToList();

      if (sceneGroups.Count == 0)
      {
        return;
      }

      if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
      {
        failed += sceneGroups.Sum(g => Fail(g, "Scene update skipped (unsaved scene changes)"));
        return;
      }

      SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

      try
      {
        foreach (IGrouping<string, Occurrence> byScene in sceneGroups)
        {
          EditorUtility.DisplayProgressBar(WindowTitle, $"Updating {byScene.Key}…", 0f);

          Scene scene = EditorSceneManager.OpenScene(byScene.Key, OpenSceneMode.Single);

          bool dirty = false;
          foreach (Occurrence occ in byScene)
          {
            IGraph machineGraph = ResolveMachineGraph(ResolveSceneTransform(scene, occ.IndexPath), occ);
            ProcessOccurrence(occ, machineGraph, ref changed, ref untouched, ref failed, ref dirty);
          }

          if (dirty)
          {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
          }
        }
      }
      finally
      {
        RestoreSceneSetup(setup);
      }
    }

    private void ProcessOccurrence(Occurrence occ, IGraph rootGraph,
        ref int changed, ref int untouched, ref int failed, ref bool dirty)
    {
      try
      {
        CheckPlatformUnit unit = rootGraph == null ? null : ResolveUnit(rootGraph, occ);
        if (unit == null)
        {
          failed++;
          occ.Error = "Node not found — rescan the project.";
          return;
        }

        if (AlignMobileWithWebGL(unit))
        {
          changed++;
          dirty = true;
        }
        else
        {
          untouched++;
        }

        RefreshStatus(occ, unit);
      }
      catch (Exception e)
      {
        failed++;
        occ.Error = e.Message;
        Debug.LogError($"[{WindowTitle}] Failed to update node in {occ.AssetPath} ({occ.GameObjectPath}): {e}");
      }
    }

    private static int Fail(IEnumerable<Occurrence> group, string message)
    {
      int count = 0;
      foreach (Occurrence occ in group)
      {
        occ.Error = message;
        count++;
      }
      return count;
    }

    #endregion

    #region Focus

    private void FocusOccurrence(Occurrence occ)
    {
      switch (occ.Kind)
      {
        case ContainerKind.GraphAsset:
          {
            UnityObject asset = AssetDatabase.LoadAssetAtPath<UnityObject>(occ.AssetPath);
            if (asset == null)
            {
              return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            OpenGraphWindow(asset, occ);
            break;
          }

        case ContainerKind.Prefab:
          {
            PrefabStage stage = PrefabStageUtility.OpenPrefab(occ.AssetPath);
            if (stage == null)
            {
              return;
            }

            Transform target = ResolveChildPath(stage.prefabContentsRoot.transform, occ.IndexPath);
            FocusMachine(target, occ);
            break;
          }

        case ContainerKind.Scene:
          {
            Scene scene = SceneManager.GetSceneByPath(occ.AssetPath);
            if (!scene.isLoaded)
            {
              if (!EditorUtility.DisplayDialog(WindowTitle, $"Open scene?\n\n{occ.AssetPath}", "Open", "Cancel")
                  || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
              {
                return;
              }

              scene = EditorSceneManager.OpenScene(occ.AssetPath, OpenSceneMode.Single);
            }

            Transform target = ResolveSceneTransform(scene, occ.IndexPath);
            FocusMachine(target, occ);
            break;
          }
      }
    }

    private void FocusMachine(Transform target, Occurrence occ)
    {
      if (target == null)
      {
        Debug.LogWarning($"[{WindowTitle}] Could not find {occ.GameObjectPath} in {occ.AssetPath} — rescan the project.");
        return;
      }

      Selection.activeGameObject = target.gameObject;
      EditorGUIUtility.PingObject(target.gameObject);

      List<MonoBehaviour> machines = MachinesOn(target);
      if (occ.MachineIndex >= 0 && occ.MachineIndex < machines.Count)
      {
        OpenGraphWindow(machines[occ.MachineIndex], occ);
      }
    }

    private static void OpenGraphWindow(UnityObject graphRootObject, Occurrence occ)
    {
      try
      {
        GraphReference reference = GraphReference.New(graphRootObject, occ.NestingPath, false);
        if (reference == null)
        {
          return;
        }

        if (reference.graph is Graph graph)
        {
          graph.pan = occ.UnitPosition;
          graph.zoom = 1f;
        }

        GraphWindow.OpenActive(reference);
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[{WindowTitle}] Could not open the graph window: {e.Message}");
      }
    }

    #endregion

    #region Hierarchy helpers

    private static List<MonoBehaviour> MachinesOn(Transform transform)
    {
      return transform.GetComponents<MonoBehaviour>()
          .Where(c => c != null && c is IMachine)
          .ToList();
    }

    private static IGraph ResolveMachineGraph(Transform target, Occurrence occ)
    {
      if (target == null)
      {
        return null;
      }

      List<MonoBehaviour> machines = MachinesOn(target);
      if (occ.MachineIndex < 0 || occ.MachineIndex >= machines.Count)
      {
        return null;
      }

      return (machines[occ.MachineIndex] as IGraphNester)?.nest?.embed;
    }

    private static string GetTransformPath(Transform transform)
    {
      List<string> segments = new();
      while (transform != null)
      {
        segments.Add(transform.name);
        transform = transform.parent;
      }

      segments.Reverse();
      return string.Join("/", segments);
    }

    /// <summary>
    /// Sibling-index chain used to find the same object again later, robust to duplicate names.
    /// For scenes (excludeRoot == null) the first entry is the root object index in the scene;
    /// for prefabs the chain starts below the prefab root.
    /// </summary>
    private static List<int> GetIndexPath(Transform transform, Transform excludeRoot)
    {
      List<int> path = new();
      while (transform != null && transform != excludeRoot)
      {
        path.Add(transform.GetSiblingIndex());
        transform = transform.parent;
      }

      path.Reverse();
      return path;
    }

    private static Transform ResolveChildPath(Transform root, List<int> indexPath)
    {
      Transform current = root;
      foreach (int index in indexPath)
      {
        if (current == null || index < 0 || index >= current.childCount)
        {
          return null;
        }

        current = current.GetChild(index);
      }

      return current;
    }

    private static Transform ResolveSceneTransform(Scene scene, List<int> indexPath)
    {
      if (indexPath.Count == 0)
      {
        return null;
      }

      GameObject[] roots = scene.GetRootGameObjects();
      if (indexPath[0] < 0 || indexPath[0] >= roots.Length)
      {
        return null;
      }

      return ResolveChildPath(roots[indexPath[0]].transform, indexPath.Skip(1).ToList());
    }

    private static void RestoreSceneSetup(SceneSetup[] setup)
    {
      if (setup != null && setup.Length > 0 && setup.All(s => !string.IsNullOrEmpty(s.path)))
      {
        EditorSceneManager.RestoreSceneManagerSetup(setup);
      }
      else
      {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      }
    }

    #endregion
  }
}
