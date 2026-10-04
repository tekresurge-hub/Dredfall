#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Dregfall.Editor
{
    [InitializeOnLoad]
    public static class DregfallSurvivorAssetSetup
    {
        const string SourcePrefab = "Assets/Shady_3d/PREFAB/Apocalyptic character (Gas Mask +hood).prefab";
        const string GeneratedFolder = "Assets/DREGFALL/Resources";
        const string GeneratedPrefab = GeneratedFolder + "/DREGFALL_SurvivorVisual.prefab";
        const string ControllerPath = GeneratedFolder + "/DREGFALL_SurvivorLocomotion.controller";

        const string IdlePath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";
        const string WalkPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx";
        const string RunPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx";
        const string SprintPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Sprint/HumanM@Sprint01_Forward.fbx";

        static DregfallSurvivorAssetSetup()
        {
            EditorApplication.delayCall += EnsureGeneratedAssets;
        }

        [MenuItem("DREGFALL/Rebuild Survivor Assets")]
        public static void EnsureGeneratedAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab) == null) return;

            EnsureFolder("Assets/DREGFALL", "Resources");

            AnimationClip idle = FindClip(IdlePath);
            AnimationClip walk = FindClip(WalkPath);
            AnimationClip run = FindClip(RunPath);
            AnimationClip sprint = FindClip(SprintPath);

            if (idle == null || walk == null || run == null || sprint == null)
            {
                Debug.LogWarning("[DREGFALL] Survivor animation clips are not ready yet. Unity will retry after the next script reload.");
                return;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);

                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                BlendTree tree = new BlendTree
                {
                    name = "DREGFALL Locomotion",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = "MoveSpeed",
                    useAutomaticThresholds = false
                };
                AssetDatabase.AddObjectToAsset(tree, controller);

                tree.AddChild(idle, 0f);
                tree.AddChild(walk, 2.2f);
                tree.AddChild(run, 4.2f);
                tree.AddChild(sprint, 7f);

                AnimatorState state = machine.AddState("Locomotion");
                state.motion = tree;
                machine.defaultState = state;
                EditorUtility.SetDirty(controller);
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "DREGFALL_SurvivorVisual";

            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            PrefabUtility.SaveAsPrefabAsset(instance, GeneratedPrefab);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DREGFALL] Survivor + idle/walk/run/sprint animation assets generated successfully.");
        }

        static AnimationClip FindClip(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
