using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CloudWhale.Game.Presentation
{
    /// <summary>Creates the complete no-asset diorama and IMGUI overlay at runtime, so Main needs no manual wiring.</summary>
    public sealed class IslandPresentationRuntime : MonoBehaviour
    {
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        private IslandPresentationController presentation;
        private OpenGameProductionRuntime productionRuntime;
        private GameObject foundation;
        private GameObject framing;
        private GameObject completedHouse;
        private GUIStyle panelStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartAutomatically()
        {
            var root = new GameObject(nameof(IslandPresentationRuntime));
            DontDestroyOnLoad(root);
            root.AddComponent<IslandPresentationRuntime>();
        }

        private IEnumerator Start()
        {
            yield return null;
            productionRuntime = FindFirstObjectByType<OpenGameProductionRuntime>();
            while (productionRuntime == null || productionRuntime.Session == null)
            {
                yield return null;
                productionRuntime = FindFirstObjectByType<OpenGameProductionRuntime>();
            }

            presentation = new IslandPresentationController(productionRuntime.Session, productionRuntime.Session.HouseFoundationCost);
            CreateDiorama();
            ApplyHouseStage();
        }

        private void Update()
        {
            if (presentation == null) return;
            presentation.Refresh();
            ApplyHouseStage();
        }

        private void OnGUI()
        {
            if (presentation == null) return;
            if (panelStyle == null) CreateStyles();
            var view = presentation.View;
            GUI.Box(new Rect(20, 20, 410, 252), GUIContent.none, panelStyle);
            GUI.Label(new Rect(38, 34, 370, 28), "Cloudwhale Island", textStyle);
            GUI.Label(new Rect(38, 70, 360, 92), ResourceText(view.Resources), textStyle);
            GUI.Label(new Rect(38, 166, 360, 26), "House: " + HouseStageText(view.HouseAppearance), textStyle);
            GUI.Label(new Rect(38, 192, 360, 42), view.NextAction, textStyle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUI.Label(new Rect(450, 20, 280, 28), "DEV · +1 each in " + productionRuntime.SecondsUntilNextProduction + "s", textStyle);
#endif
            if (view.CanBuildNextHouseStage && GUI.Button(new Rect(38, 238, 190, 38), "Build next house stage", buttonStyle))
            {
                presentation.BuildNextHouseStage();
                ApplyHouseStage();
            }

            GUI.Box(new Rect(20, Screen.height - 62, 560, 42), view.StatusMessage, panelStyle);
        }

        private static string ResourceText(ResourceAmounts r) =>
            "Driftwood  " + r.Driftwood + "\nCloud Cotton  " + r.CloudCotton + "\nDew  " + r.Dew + "\nStardust  " + r.Stardust;

        private static string HouseStageText(IslandHouseAppearance appearance)
        {
            switch (appearance)
            {
                case IslandHouseAppearance.Foundation: return "Foundation";
                case IslandHouseAppearance.Framing: return "Framing";
                case IslandHouseAppearance.Complete: return "Complete";
                default: return "Unbuilt";
            }
        }

        private void CreateDiorama()
        {
            RenderSettings.ambientLight = new Color(0.52f, 0.67f, 0.77f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.63f, 0.82f, 0.9f);
            RenderSettings.fogDensity = 0.012f;
            Camera.main?.gameObject.SetActive(false);
            var camera = new GameObject("Fixed Quarter View Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(12, 11, -15);
            camera.transform.LookAt(new Vector3(0, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.57f, 0.79f, 0.91f);
            camera.fieldOfView = 42;
            var light = new GameObject("Soft Sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(48, -32, 0);

            CreateSkyWhale();
            CreateIsland();
            CreateCloud(new Vector3(-5f, 4f, 4f), 1.1f, "Cloud One");
            CreateCloud(new Vector3(5f, 3f, 5f), 0.9f, "Cloud Two");
            CreatePineTree(new Vector3(-2.1f, 0.75f, 0.3f), 1.05f);
            CreatePineTree(new Vector3(2.6f, 0.7f, -0.75f), 0.7f);
            foundation = CreateHouseFoundation(new Vector3(0.65f, 0.76f, 0.2f));
            framing = CreateHouseFraming(new Vector3(0.65f, 0.76f, 0.2f));
            completedHouse = CreateCompletedHouse(new Vector3(0.65f, 0.76f, 0.2f));
        }

        private void ApplyHouseStage()
        {
            if (presentation == null) return;
            var appearance = presentation.View.HouseAppearance;
            if (foundation != null) foundation.SetActive(appearance == IslandHouseAppearance.Foundation);
            if (framing != null) framing.SetActive(appearance == IslandHouseAppearance.Framing);
            if (completedHouse != null) completedHouse.SetActive(appearance == IslandHouseAppearance.Complete);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name; item.transform.position = position; item.transform.localScale = scale;
            var renderer = item.GetComponent<Renderer>();
            var material = LoadMaterialFor(name);
            if (material != null)
            {
                // A serialized material keeps its shader in the Web build; a material created only at runtime can be stripped.
                renderer.sharedMaterial = material;
            }
            else
            {
                renderer.material.color = color;
            }
            return item;
        }

        private static Material LoadMaterialFor(string objectName)
        {
            var textureName = objectName.StartsWith("Whale ") && !objectName.Contains("Eye") && !objectName.Contains("Spout")
                ? "whale-skin"
                : objectName == "Island Meadow" || objectName == "Pine Foliage"
                    ? "meadow"
                    : objectName == "Island Soil"
                        ? "island-soil"
                        : objectName == "Tree Trunk" || objectName.StartsWith("Foundation ") || objectName.StartsWith("Framing ")
                            ? "warm-wood"
                            : null;

            if (textureName == null) return null;
            if (MaterialCache.TryGetValue(textureName, out var cached)) return cached;

            var material = Resources.Load<Material>("Materials/" + textureName);
            MaterialCache.Add(textureName, material);
            return material;
        }

        private static GameObject CreateRoot(string name)
        {
            return new GameObject(name);
        }

        private void CreateSkyWhale()
        {
            var root = CreateRoot("Sleeping Sky Whale Model");
            var blue = new Color(0.22f, 0.48f, 0.72f);
            var belly = new Color(0.73f, 0.88f, 0.94f);
            var fin = new Color(0.16f, 0.36f, 0.58f);
            Parent(CreatePrimitive(PrimitiveType.Sphere, "Whale Body", new Vector3(0f, -1.55f, 0.4f), new Vector3(7.8f, 2.7f, 4.5f), blue), root);
            Parent(CreatePrimitive(PrimitiveType.Sphere, "Whale Belly", new Vector3(0.1f, -1.1f, 0.22f), new Vector3(6.9f, 1.5f, 3.6f), belly), root);
            Parent(CreatePrimitive(PrimitiveType.Sphere, "Whale Head", new Vector3(-3.35f, -1.3f, 0.42f), new Vector3(2.5f, 2.25f, 3.2f), blue), root);
            var leftEye = CreatePrimitive(PrimitiveType.Sphere, "Whale Left Eye", new Vector3(-4.15f, -0.8f, -0.75f), new Vector3(0.23f, 0.23f, 0.23f), new Color(0.04f, 0.08f, 0.15f));
            Parent(leftEye, root);
            var rightEye = CreatePrimitive(PrimitiveType.Sphere, "Whale Right Eye", new Vector3(-4.15f, -0.8f, 1.58f), new Vector3(0.23f, 0.23f, 0.23f), new Color(0.04f, 0.08f, 0.15f));
            Parent(rightEye, root);
            var leftFin = CreatePrimitive(PrimitiveType.Sphere, "Whale Left Fin", new Vector3(0.45f, -1.35f, -2.1f), new Vector3(2.15f, 0.36f, 1.1f), fin);
            leftFin.transform.rotation = Quaternion.Euler(15f, 10f, 22f); Parent(leftFin, root);
            var rightFin = CreatePrimitive(PrimitiveType.Sphere, "Whale Right Fin", new Vector3(0.45f, -1.35f, 2.9f), new Vector3(2.15f, 0.36f, 1.1f), fin);
            rightFin.transform.rotation = Quaternion.Euler(-15f, 10f, 22f); Parent(rightFin, root);
            var tailLeft = CreatePrimitive(PrimitiveType.Sphere, "Whale Tail Left", new Vector3(4.05f, -1.45f, -0.85f), new Vector3(1.7f, 0.35f, 1.25f), fin);
            tailLeft.transform.rotation = Quaternion.Euler(0f, 0f, 28f); Parent(tailLeft, root);
            var tailRight = CreatePrimitive(PrimitiveType.Sphere, "Whale Tail Right", new Vector3(4.05f, -1.45f, 1.65f), new Vector3(1.7f, 0.35f, 1.25f), fin);
            tailRight.transform.rotation = Quaternion.Euler(0f, 0f, -28f); Parent(tailRight, root);
            Parent(CreatePrimitive(PrimitiveType.Sphere, "Whale Spout", new Vector3(-2.5f, 0.2f, 0.4f), new Vector3(0.38f, 0.8f, 0.38f), Color.white), root);
        }

        private void CreateIsland()
        {
            var root = CreateRoot("Floating Island Model");
            Parent(CreatePrimitive(PrimitiveType.Sphere, "Island Meadow", new Vector3(0f, 0.1f, 0f), new Vector3(7.8f, 1.25f, 5.4f), new Color(0.31f, 0.66f, 0.39f)), root);
            Parent(CreatePrimitive(PrimitiveType.Sphere, "Island Soil", new Vector3(0f, -0.35f, 0f), new Vector3(7.0f, 1.15f, 4.75f), new Color(0.45f, 0.28f, 0.16f)), root);
            for (var i = 0; i < 7; i++)
            {
                var angle = i * Mathf.PI * 2f / 7f;
                var rock = CreatePrimitive(PrimitiveType.Sphere, "Island Rock", new Vector3(Mathf.Cos(angle) * 3.3f, 0.23f, Mathf.Sin(angle) * 2.1f), new Vector3(0.55f, 0.36f, 0.48f), new Color(0.58f, 0.63f, 0.67f));
                rock.transform.rotation = Quaternion.Euler(0f, i * 37f, 25f);
                Parent(rock, root);
            }
        }

        private void CreateCloud(Vector3 center, float scale, string name)
        {
            var root = CreateRoot(name + " Model");
            Parent(CreatePrimitive(PrimitiveType.Sphere, name + " Base", center, new Vector3(2.8f, 0.75f, 1.25f) * scale, Color.white), root);
            Parent(CreatePrimitive(PrimitiveType.Sphere, name + " Puff A", center + new Vector3(-0.7f, 0.42f, 0f) * scale, new Vector3(1.25f, 1.05f, 1f) * scale, Color.white), root);
            Parent(CreatePrimitive(PrimitiveType.Sphere, name + " Puff B", center + new Vector3(0.35f, 0.58f, 0.05f) * scale, new Vector3(1.5f, 1.25f, 1.05f) * scale, Color.white), root);
        }

        private void CreatePineTree(Vector3 basePosition, float scale)
        {
            var root = CreateRoot("Soft Pine Tree Model");
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Tree Trunk", basePosition + Vector3.up * (0.72f * scale), new Vector3(0.22f, 0.72f, 0.22f) * scale, new Color(0.32f, 0.18f, 0.09f)), root);
            CreateFoliageTier(root, basePosition + Vector3.up * (1.22f * scale), 1.2f * scale, new Color(0.18f, 0.45f, 0.27f));
            CreateFoliageTier(root, basePosition + Vector3.up * (1.72f * scale), 0.95f * scale, new Color(0.21f, 0.53f, 0.31f));
            CreateFoliageTier(root, basePosition + Vector3.up * (2.15f * scale), 0.68f * scale, new Color(0.25f, 0.6f, 0.35f));
        }

        private static void CreateFoliageTier(GameObject root, Vector3 position, float radius, Color color)
        {
            var tier = CreatePrimitive(PrimitiveType.Sphere, "Pine Foliage", position, new Vector3(radius, radius * 0.62f, radius), color);
            Parent(tier, root);
        }

        private GameObject CreateHouseFoundation(Vector3 center)
        {
            var root = CreateRoot("House Foundation Model");
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "House Clearing", center, new Vector3(2.2f, 0.16f, 2.2f), new Color(0.8f, 0.63f, 0.39f)), root);
            var wood = new Color(0.59f, 0.35f, 0.17f);
            for (var row = -1; row <= 1; row++)
            {
                var plank = CreatePrimitive(PrimitiveType.Cube, "Foundation Plank", center + new Vector3(0f, 0.31f, row * 0.48f), new Vector3(1.95f, 0.16f, 0.37f), wood);
                Parent(plank, root);
            }
            foreach (var offset in new[] { new Vector3(-0.8f, 0.5f, -0.56f), new Vector3(0.8f, 0.5f, -0.56f), new Vector3(-0.8f, 0.5f, 0.56f), new Vector3(0.8f, 0.5f, 0.56f) })
            {
                Parent(CreatePrimitive(PrimitiveType.Cylinder, "Foundation Post", center + offset, new Vector3(0.12f, 0.36f, 0.12f), new Color(0.39f, 0.22f, 0.1f)), root);
            }
            return root;
        }

        private GameObject CreateHouseFraming(Vector3 center)
        {
            var root = CreateRoot("House Framing Model");
            var wood = new Color(0.5f, 0.28f, 0.12f);
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Framing Clearing", center, new Vector3(2.2f, 0.16f, 2.2f), new Color(0.8f, 0.63f, 0.39f)), root);
            foreach (var offset in new[]
            {
                new Vector3(-0.88f, 1.25f, -0.72f), new Vector3(0.88f, 1.25f, -0.72f),
                new Vector3(-0.88f, 1.25f, 0.72f), new Vector3(0.88f, 1.25f, 0.72f),
            })
            {
                Parent(CreatePrimitive(PrimitiveType.Cylinder, "Framing Tall Post", center + offset, new Vector3(0.14f, 1.18f, 0.14f), wood), root);
            }
            Parent(CreatePrimitive(PrimitiveType.Cube, "Framing Front Beam", center + new Vector3(0f, 2.38f, -0.72f), new Vector3(2.05f, 0.18f, 0.18f), wood), root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Framing Back Beam", center + new Vector3(0f, 2.38f, 0.72f), new Vector3(2.05f, 0.18f, 0.18f), wood), root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Framing Roof Ridge", center + new Vector3(0f, 2.9f, 0f), new Vector3(0.2f, 0.18f, 1.8f), wood), root);
            return root;
        }

        private GameObject CreateCompletedHouse(Vector3 center)
        {
            var root = CreateRoot("Completed House Model");
            var wall = new Color(0.95f, 0.82f, 0.57f);
            var roof = new Color(0.64f, 0.22f, 0.18f);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Completed House Walls", center + new Vector3(0f, 1.15f, 0f), new Vector3(2.05f, 1.5f, 1.65f), wall), root);
            var roofObject = CreatePrimitive(PrimitiveType.Cube, "Completed House Roof", center + new Vector3(0f, 2.25f, 0f), new Vector3(2.45f, 0.52f, 2.05f), roof);
            roofObject.transform.rotation = Quaternion.Euler(0f, 0f, 8f);
            Parent(roofObject, root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Completed House Door", center + new Vector3(0f, 0.92f, -0.84f), new Vector3(0.42f, 0.8f, 0.08f), new Color(0.32f, 0.16f, 0.07f)), root);
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Completed House Chimney", center + new Vector3(0.65f, 2.75f, 0.2f), new Vector3(0.2f, 0.48f, 0.2f), new Color(0.42f, 0.35f, 0.31f)), root);
            return root;
        }

        private static void Parent(GameObject child, GameObject root)
        {
            child.transform.SetParent(root.transform, true);
        }

        private void CreateStyles()
        {
            panelStyle = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 10, 10) };
            panelStyle.normal.background = MakeTexture(new Color(0.05f, 0.14f, 0.22f, 0.8f)); panelStyle.normal.textColor = Color.white;
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }
    }
}
