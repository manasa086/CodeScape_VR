using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CodeScape.EditorTools
{
    // Menu: CodeScape > Build Scene
    // Generates the whole CodeScape scene from code, so the project does not depend on a scene file
    // saved by a newer Unity editor. Nodes, lines and UI panels are created at runtime from
    // Resources/architecture.json; this builder only places the room, table, rig, anchors and managers.
    public static class SceneBuilder
    {
        const string Root = "Assets/CodeScape";
        const string MaterialsDir = Root + "/Materials";
        const string ScenesDir = Root + "/Scenes";
        const string ScenePath = ScenesDir + "/CodeScape.unity";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Room layout (meters). The user stands at the origin facing +Z.
        const float TableHeight = 0.75f;
        static readonly Vector3 TableCenter = new Vector3(0f, TableHeight, 0.95f);
        static readonly Vector3 TableSize = new Vector3(1.7f, 0.05f, 1.1f);
        static readonly Vector3 EyePoint = new Vector3(0f, 1.6f, 0f);

        [MenuItem("CodeScape/Build Scene", priority = 0)]
        public static void BuildFromMenu()
        {
            // The CodeScape scene is regenerated anyway, so only ask about saving other scenes.
            bool rebuildingSelf = EditorSceneManager.GetActiveScene().path == ScenePath;
            if (!rebuildingSelf && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("CodeScape/Open Scene", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("CodeScape", "The scene has not been built yet. Use CodeScape > Build Scene.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(ScenesDir);
            var m = CreateMaterials();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) font = TMP_Settings.defaultFontAsset;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting(m.Sky);
            BuildEnvironment(m);
            var rig = BuildCameraRig();

            var architectureRoot = new GameObject("ArchitectureRoot").transform;
            architectureRoot.position = new Vector3(0.15f, TableHeight + TableSize.y * 0.5f, 1.0f);

            var ui = new GameObject("UI").transform;
            var infoAnchor = CreateFacingAnchor(ui, "InfoPanelAnchor", new Vector3(0.98f, 1.22f, 1.0f));
            var boardAnchor = CreateFacingAnchor(ui, "StatusBoardAnchor", new Vector3(0f, 1.26f, 1.64f));
            var consoleAnchor = new GameObject("ConsoleAnchor").transform;
            consoleAnchor.SetParent(ui, false);
            consoleAnchor.position = new Vector3(0f, TableHeight + TableSize.y * 0.5f + 0.012f, 0.52f);
            consoleAnchor.rotation = Quaternion.Euler(-15f, 0f, 0f);   // back edge raised, like a lectern
            CreateBlock(consoleAnchor, "ConsoleBase", new Vector3(0f, -0.006f, 0f), new Vector3(0.9f, 0.012f, 0.11f), m.Console);

            BuildManagers(m, font, architectureRoot, infoAnchor, boardAnchor, consoleAnchor, rig);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();

            var view = SceneView.lastActiveSceneView;
            if (view != null) view.LookAt(new Vector3(0.1f, 0.95f, 1.0f), Quaternion.Euler(28f, 0f, 0f), 1.9f);

            Debug.Log($"[CodeScape] Scene built and saved to {ScenePath}. Press Play to try it " +
                      "(desktop: left-click to select, right-drag to look, H/T/F/R keys).");
        }

        // ---------------- materials ----------------

        class Materials
        {
            public Material Node, Glow, Line, Panel, Button, Floor, Wall, Table, Leg, Console, Sky;
        }

        static Materials CreateMaterials()
        {
            const string lit = "Universal Render Pipeline/Lit";
            const string unlit = "Universal Render Pipeline/Unlit";
            var m = new Materials
            {
                Node    = GetOrCreateMaterial("Node", lit, Color.white, 0.55f, emissive: true),
                Button  = GetOrCreateMaterial("Button", lit, new Color(0.2f, 0.22f, 0.26f), 0.4f, emissive: true),
                Glow    = GetOrCreateMaterial("Glow", unlit, new Color(0.2f, 0.9f, 0.95f)),
                Panel   = GetOrCreateMaterial("Panel", unlit, new Color(0.06f, 0.07f, 0.1f)),
                Line    = GetOrCreateMaterial("Line", "Sprites/Default", Color.white),
                Floor   = GetOrCreateMaterial("Floor", lit, new Color(0.16f, 0.17f, 0.2f), 0.25f),
                Wall    = GetOrCreateMaterial("Wall", lit, new Color(0.3f, 0.33f, 0.38f), 0.1f),
                Table   = GetOrCreateMaterial("Table", lit, new Color(0.13f, 0.14f, 0.17f), 0.7f),
                Leg     = GetOrCreateMaterial("TableLeg", lit, new Color(0.35f, 0.36f, 0.4f), 0.6f),
                Console = GetOrCreateMaterial("Console", lit, new Color(0.08f, 0.09f, 0.11f), 0.5f),
                Sky     = GetOrCreateMaterial("Sky", "Skybox/Procedural", Color.white),
            };

            m.Sky.SetFloat("_SunSize", 0.02f);
            m.Sky.SetFloat("_AtmosphereThickness", 0.55f);
            m.Sky.SetColor("_SkyTint", new Color(0.32f, 0.38f, 0.5f));
            m.Sky.SetColor("_GroundColor", new Color(0.12f, 0.12f, 0.14f));
            m.Sky.SetFloat("_Exposure", 0.9f);
            EditorUtility.SetDirty(m.Sky);
            return m;
        }

        static Material GetOrCreateMaterial(string name, string shaderName, Color color, float smoothness = 0.5f,
                                            bool emissive = false)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[CodeScape] Shader '{shaderName}' not found, using URP Lit for {name}.");
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                // Enabled on the asset so the emission shader variant is kept in Quest builds.
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------- scene content ----------------

        static void SetupLighting(Material sky)
        {
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.37f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.15f, 0.17f);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildEnvironment(Materials m)
        {
            var env = new GameObject("Environment").transform;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env, false);
            floor.GetComponent<Renderer>().sharedMaterial = m.Floor;

            // Four low-contrast walls give the room a sense of scale in the headset.
            const float half = 3.5f, height = 3f, thick = 0.1f;
            CreateBlock(env, "Wall_Front", new Vector3(0f, height / 2f, half), new Vector3(half * 2f, height, thick), m.Wall);
            CreateBlock(env, "Wall_Back", new Vector3(0f, height / 2f, -half), new Vector3(half * 2f, height, thick), m.Wall);
            CreateBlock(env, "Wall_Left", new Vector3(-half, height / 2f, 0f), new Vector3(thick, height, half * 2f), m.Wall);
            CreateBlock(env, "Wall_Right", new Vector3(half, height / 2f, 0f), new Vector3(thick, height, half * 2f), m.Wall);

            var table = new GameObject("Table").transform;
            table.SetParent(env, false);
            CreateBlock(table, "Top", TableCenter, TableSize, m.Table);
            float legH = TableHeight - TableSize.y * 0.5f;
            float lx = TableSize.x * 0.5f - 0.06f, lz = TableSize.z * 0.5f - 0.06f;
            foreach (var (x, z) in new[] { (-lx, -lz), (lx, -lz), (-lx, lz), (lx, lz) })
                CreateBlock(table, "Leg", new Vector3(TableCenter.x + x, legH * 0.5f, TableCenter.z + z),
                            new Vector3(0.05f, legH, 0.05f), m.Leg);
        }

        static GameObject CreateBlock(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Transform CreateFacingAnchor(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            // TextMeshPro and quads are read from their -Z side, so +Z points away from the viewer.
            t.rotation = Quaternion.LookRotation(position - EyePoint, Vector3.up);
            return t;
        }

        class RigRefs
        {
            public Transform Left, Right;
        }

        static RigRefs BuildCameraRig()
        {
            var refs = new RigRefs();
            var prefab = FindPrefab("OVRCameraRig");
            if (prefab == null)
            {
                Debug.LogWarning("[CodeScape] OVRCameraRig prefab not found (Meta XR Core SDK missing?). Using a plain camera.");
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 1.6f, 0f);
                return refs;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = "OVRCameraRig";
            rig.transform.position = Vector3.zero;

            // Floor-level tracking keeps the table at a real 75 cm height for the standing user.
            var manager = rig.GetComponent<OVRManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                var origin = so.FindProperty("_trackingOriginType");
                if (origin != null)
                {
                    origin.intValue = (int)OVRManager.TrackingOrigin.FloorLevel;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            refs.Left = rig.transform.Find("TrackingSpace/LeftHandAnchor/LeftControllerAnchor");
            refs.Right = rig.transform.Find("TrackingSpace/RightHandAnchor/RightControllerAnchor");
            AddControllerModel(refs.Left, OVRInput.Controller.LTouch);
            AddControllerModel(refs.Right, OVRInput.Controller.RTouch);
            return refs;
        }

        static void AddControllerModel(Transform anchor, OVRInput.Controller controller)
        {
            if (anchor == null) return;
            var prefab = FindPrefab("OVRControllerPrefab");
            if (prefab == null) return;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var helper = model.GetComponent<OVRControllerHelper>();
            if (helper != null)
            {
                var so = new SerializedObject(helper);
                var prop = so.FindProperty("m_controller");
                if (prop != null)
                {
                    prop.intValue = (int)controller;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        static void BuildManagers(Materials m, TMP_FontAsset font, Transform architectureRoot,
                                  Transform infoAnchor, Transform boardAnchor, Transform consoleAnchor, RigRefs rig)
        {
            var managers = new GameObject("Managers");

            var arch = managers.AddComponent<ArchitectureManager>();
            SetFields(arch, new Dictionary<string, Object>
            {
                { "architectureJson", AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Resources/architecture.json") },
                { "architectureRoot", architectureRoot },
                { "nodeMaterial", m.Node },
                { "glowMaterial", m.Glow },
                { "lineMaterial", m.Line },
                { "font", font },
            });

            managers.AddComponent<SelectionManager>();

            var ui = managers.AddComponent<UIManager>();
            SetFields(ui, new Dictionary<string, Object>
            {
                { "infoPanelAnchor", infoAnchor },
                { "statusBoardAnchor", boardAnchor },
                { "consoleAnchor", consoleAnchor },
                { "panelMaterial", m.Panel },
                { "buttonMaterial", m.Button },
            });

            managers.AddComponent<RequestSimulator>();
            managers.AddComponent<AudioFeedback>();
            managers.AddComponent<DesktopInputFallback>();

            CreateRay(managers.transform, "LeftControllerRay", OVRInput.Controller.LTouch, rig.Left, m.Line);
            CreateRay(managers.transform, "RightControllerRay", OVRInput.Controller.RTouch, rig.Right, m.Line);
        }

        static void CreateRay(Transform parent, string name, OVRInput.Controller controller, Transform origin, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ray = go.AddComponent<ControllerRaySelector>();
            var so = new SerializedObject(ray);
            so.FindProperty("controller").intValue = (int)controller;
            so.FindProperty("rayOrigin").objectReferenceValue = origin;
            so.FindProperty("laserMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFields(Object target, Dictionary<string, Object> values)
        {
            var so = new SerializedObject(target);
            foreach (var pair in values)
            {
                var prop = so.FindProperty(pair.Key);
                if (prop == null) Debug.LogWarning($"[CodeScape] Field '{pair.Key}' not found on {target.GetType().Name}.");
                else prop.objectReferenceValue = pair.Value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- helpers ----------------

        static GameObject FindPrefab(string prefabName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == prefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
