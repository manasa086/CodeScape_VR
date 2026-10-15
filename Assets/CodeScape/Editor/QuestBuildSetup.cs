using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CodeScape.EditorTools
{
    // Menu: CodeScape > Quest
    // Applies the Android settings the Meta Quest 2 needs (Meta XR SDK 201 requires Android API level 32+)
    // and builds the APK, so the same build can be reproduced on any lab machine.
    public static class QuestBuildSetup
    {
        const string ScenePath = "Assets/CodeScape/Scenes/CodeScape.unity";
        const string ApkPath = "Builds/CodeScapeVR.apk";
        const string PackageId = "com.team2.codescapevr";

        // Render pipeline assets created by this Unity version. The template's URP assets were saved by a newer
        // URP (Unity 6000.6) and are rejected by the 6000.0.40f1 build ("not at last version").
        const string RenderingDir = "Assets/CodeScape/Rendering";
        const string PipelinePath = RenderingDir + "/CodeScape_URP.asset";
        const string RendererPath = RenderingDir + "/CodeScape_URP_Renderer.asset";
        const string GlobalSettingsPath = RenderingDir + "/CodeScape_URPGlobalSettings.asset";

        [MenuItem("CodeScape/Quest/Configure Android Settings", priority = 20)]
        public static void Configure()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Team2";
            PlayerSettings.productName = "CodeScape VR";
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EnsureOpenXRLoader();
            EnsureRenderPipeline();
            AssetDatabase.SaveAssets();

            Debug.Log($"[CodeScape] Quest Android settings applied: {PackageId}, min API 32, target API 34, " +
                      "IL2CPP ARM64, Vulkan, ASTC.");
        }

        // The XR settings file was saved by a newer XR Management version, whose per-platform list this version
        // cannot read, so XR was not enabled for Android. Re-link the existing Android settings and make sure
        // OpenXR is the active loader, otherwise the APK starts as a flat 2D app.
        static void EnsureOpenXRLoader()
        {
            const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if (perTarget == null)
            {
                Debug.LogWarning("[CodeScape] XR settings asset not found; enable OpenXR for Android in Project Settings > XR Plug-in Management.");
                return;
            }

            if (perTarget.SettingsForBuildTarget(BuildTargetGroup.Android) == null)
            {
                var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.XR.Management.XRGeneralSettings>()
                    .FirstOrDefault(s => s.name == "Android Settings");
                if (existing != null) perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, existing);
                else perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            android.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(android.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Android);
            EditorUtility.SetDirty(android);
            EditorUtility.SetDirty(perTarget);

            // Recommended by Meta's Project Setup Tool: subsampled layout lowers GPU cost when foveation is on.
            var openXR = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR != null)
            {
                foreach (var feature in openXR.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                {
                    if (feature.GetType().Name != "MetaXRSubsampledLayout") continue;
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);   // also persists a change made in memory only
                }
            }
        }

        // Uses one URP asset (created by this editor version, tuned for Quest) for every quality level and the
        // Graphics settings, plus matching URP global settings.
        static void EnsureRenderPipeline()
        {
            if (!AssetDatabase.IsValidFolder(RenderingDir))
                AssetDatabase.CreateFolder("Assets/CodeScape", "Rendering");

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(CreateRendererData());
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 8f;
            pipeline.shadowCascadeCount = 1;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentLevel, false);

            // UniversalRenderPipelineGlobalSettings is internal in this URP version, so it is created by type.
            var globalSettings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalSettingsPath);
            if (globalSettings == null)
            {
                var settingsType = typeof(UniversalRenderPipeline).Assembly
                    .GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
                globalSettings = RenderPipelineGlobalSettingsUtils.Create(settingsType, GlobalSettingsPath);
            }
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(globalSettings);

            // URP creates its default volume profile in the Assets root; keep it with the other rendering assets.
            const string rootProfile = "Assets/DefaultVolumeProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(rootProfile) != null)
                AssetDatabase.MoveAsset(rootProfile, RenderingDir + "/CodeScape_DefaultVolumeProfile.asset");
        }

        static ScriptableRendererData CreateRendererData()
        {
            // URP's own (internal) helper also assigns the default post-processing resources.
            var helper = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
            if (helper != null)
                return (ScriptableRendererData)helper.Invoke(null, new object[] { RendererPath, RendererType.UniversalRenderer, false, "Renderer" });

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
            return data;
        }

        [MenuItem("CodeScape/Quest/Build APK", priority = 21)]
        public static void BuildApk() => Build(false);

        [MenuItem("CodeScape/Quest/Build and Run on Quest", priority = 22)]
        public static void BuildAndRun() => Build(true);

        static void Build(bool runOnDevice)
        {
            Configure();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = runOnDevice ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[CodeScape] APK built: {ApkPath} ({summary.totalSize / (1024 * 1024)} MB) " +
                          $"in {summary.totalTime:mm\\:ss}.");
            else
                Debug.LogError($"[CodeScape] Android build {summary.result} with {summary.totalErrors} error(s). " +
                               "See the Console for details.");
        }
    }
}
