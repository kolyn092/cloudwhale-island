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
        private GameObject lockedGarden;
        private GameObject gardenFoundation;
        private GameObject gardenFraming;
        private GameObject completedGarden;
        private GUIStyle panelStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private Camera orbitCamera;
        private readonly IslandOrbitDrag orbit = new IslandOrbitDrag();
        private float orbitDistance;

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
            ApplyGardenStage();
        }

        private void Update()
        {
            if (presentation == null) return;
            presentation.Refresh();
            ApplyHouseStage();
            ApplyGardenStage();
        }

        private void OnGUI()
        {
            if (presentation == null) return;
            if (panelStyle == null) CreateStyles();
            var pointer = Event.current;
            var overPanel = new Rect(20, 20, 410, 338).Contains(pointer.mousePosition)
                || new Rect(20, Screen.height - 62, 560, 42).Contains(pointer.mousePosition);
            if (pointer.button == 0 && orbit.Handle(pointer.type, pointer.delta, overPanel))
            {
                ApplyCameraOrbit();
                pointer.Use();
            }
            GUI.Label(new Rect(Screen.width - 290, Screen.height - 100, 270, 32), "Drag the island to look around", textStyle);
            var view = presentation.View;
            GUI.Box(new Rect(20, 20, 410, 338), GUIContent.none, panelStyle);
            GUI.Label(new Rect(38, 34, 370, 28), "Cloudwhale Island", textStyle);
            GUI.Label(new Rect(38, 70, 360, 92), ResourceText(view.Resources), textStyle);
            GUI.Label(new Rect(38, 166, 360, 26), "House: " + HouseStageText(view.HouseAppearance), textStyle);
            GUI.Label(new Rect(38, 192, 360, 42), view.NextAction, textStyle);
            GUI.Label(new Rect(38, 238, 360, 26), "Garden: " + GardenStageText(view.GardenAppearance), textStyle);
            GUI.Label(new Rect(38, 264, 360, 42), view.GardenNextAction, textStyle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUI.Label(new Rect(450, 20, 350, 46), ProductionText(view.GardenAppearance) + " in " + productionRuntime.SecondsUntilNextProduction + "s", textStyle);
#endif
            if (view.CanBuildNextHouseStage && GUI.Button(new Rect(38, 310, 175, 38), "Build next house stage", buttonStyle))
            {
                presentation.BuildNextHouseStage();
                ApplyHouseStage();
                ApplyGardenStage();
            }

            if (view.CanBuildNextGardenStage && GUI.Button(new Rect(223, 310, 175, 38), "Build next garden stage", buttonStyle))
            {
                presentation.BuildNextGardenStage();
                ApplyGardenStage();
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

        private static string GardenStageText(IslandGardenAppearance appearance)
        {
            switch (appearance)
            {
                case IslandGardenAppearance.Foundation: return "Foundation";
                case IslandGardenAppearance.Framing: return "Framing";
                case IslandGardenAppearance.Complete: return "Complete";
                default: return "Locked vacant lot";
            }
        }

        private static string ProductionText(IslandGardenAppearance appearance)
        {
            return appearance == IslandGardenAppearance.Complete
                ? "DEV · Driftwood +1 · Cloud Cotton +2 · Dew +2 · Stardust +1"
                : "DEV · +1 each";
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
            orbitCamera = camera;
            orbitDistance = camera.transform.position.magnitude;
            orbit.Yaw = camera.transform.eulerAngles.y;
            orbit.Pitch = camera.transform.eulerAngles.x;
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
            var gardenCenter = new Vector3(-0.7f, 0.76f, 2.1f);
            lockedGarden = CreateLockedGarden(gardenCenter);
            gardenFoundation = CreateGardenFoundation(gardenCenter);
            gardenFraming = CreateGardenFraming(gardenCenter);
            completedGarden = CreateCompletedGarden(gardenCenter);
        }

        private void ApplyHouseStage()
        {
            if (presentation == null) return;
            var appearance = presentation.View.HouseAppearance;
            if (foundation != null) foundation.SetActive(appearance == IslandHouseAppearance.Foundation);
            if (framing != null) framing.SetActive(appearance == IslandHouseAppearance.Framing);
            if (completedHouse != null) completedHouse.SetActive(appearance == IslandHouseAppearance.Complete);
        }

        private void ApplyCameraOrbit()
        {
            if (orbitCamera == null) return;
            var rotation = Quaternion.Euler(orbit.Pitch, orbit.Yaw, 0);
            orbitCamera.transform.SetPositionAndRotation(rotation * Vector3.back * orbitDistance, rotation);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) orbit.Cancel();
        }

        private void OnDisable() => orbit.Cancel();

        private void ApplyGardenStage()
        {
            if (presentation == null) return;
            var appearance = presentation.View.GardenAppearance;
            if (lockedGarden != null) lockedGarden.SetActive(appearance == IslandGardenAppearance.Locked);
            if (gardenFoundation != null) gardenFoundation.SetActive(appearance == IslandGardenAppearance.Foundation);
            if (gardenFraming != null) gardenFraming.SetActive(appearance == IslandGardenAppearance.Framing);
            if (completedGarden != null) completedGarden.SetActive(appearance == IslandGardenAppearance.Complete);
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

        private GameObject CreateLockedGarden(Vector3 center)
        {
            var root = CreateRoot("Garden Locked Vacant Lot Model");
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Garden Locked Clearing", center, new Vector3(2.05f, 0.12f, 1.55f), new Color(0.55f, 0.4f, 0.22f)), root);
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Garden Locked Sign Post", center + new Vector3(-0.72f, 0.5f, 0f), new Vector3(0.08f, 0.48f, 0.08f), new Color(0.36f, 0.2f, 0.09f)), root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Garden Locked Sign", center + new Vector3(-0.72f, 0.88f, 0f), new Vector3(0.62f, 0.32f, 0.08f), new Color(0.68f, 0.47f, 0.2f)), root);
            return root;
        }

        private GameObject CreateGardenFoundation(Vector3 center)
        {
            var root = CreateRoot("Garden Foundation Model");
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Garden Foundation Soil", center, new Vector3(2.05f, 0.12f, 1.55f), new Color(0.39f, 0.23f, 0.1f)), root);
            foreach (var offset in new[] { -0.55f, 0.55f })
            {
                Parent(CreatePrimitive(PrimitiveType.Cube, "Garden Foundation Bed", center + new Vector3(offset, 0.18f, 0f), new Vector3(0.72f, 0.13f, 1.65f), new Color(0.63f, 0.36f, 0.16f)), root);
            }
            return root;
        }

        private GameObject CreateGardenFraming(Vector3 center)
        {
            var root = CreateRoot("Garden Framing Model");
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Garden Framing Soil", center, new Vector3(2.05f, 0.12f, 1.55f), new Color(0.39f, 0.23f, 0.1f)), root);
            foreach (var offset in new[] { new Vector3(-0.82f, 0.75f, -0.62f), new Vector3(0.82f, 0.75f, -0.62f), new Vector3(-0.82f, 0.75f, 0.62f), new Vector3(0.82f, 0.75f, 0.62f) })
            {
                Parent(CreatePrimitive(PrimitiveType.Cylinder, "Garden Framing Post", center + offset, new Vector3(0.08f, 0.7f, 0.08f), new Color(0.39f, 0.22f, 0.1f)), root);
            }
            Parent(CreatePrimitive(PrimitiveType.Cube, "Garden Framing Beam", center + new Vector3(0f, 1.35f, -0.62f), new Vector3(1.78f, 0.1f, 0.1f), new Color(0.39f, 0.22f, 0.1f)), root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Garden Framing Back Beam", center + new Vector3(0f, 1.35f, 0.62f), new Vector3(1.78f, 0.1f, 0.1f), new Color(0.39f, 0.22f, 0.1f)), root);
            return root;
        }

        private GameObject CreateCompletedGarden(Vector3 center)
        {
            var root = CreateRoot("Completed Garden Model");
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Completed Garden Soil", center, new Vector3(2.05f, 0.12f, 1.55f), new Color(0.39f, 0.23f, 0.1f)), root);
            foreach (var offset in new[] { new Vector3(-0.56f, 0.34f, -0.45f), new Vector3(0.5f, 0.34f, -0.32f), new Vector3(-0.25f, 0.34f, 0.5f), new Vector3(0.7f, 0.34f, 0.55f) })
            {
                Parent(CreatePrimitive(PrimitiveType.Sphere, "Garden Bloom", center + offset, new Vector3(0.48f, 0.55f, 0.48f), new Color(0.36f, 0.72f, 0.3f)), root);
                Parent(CreatePrimitive(PrimitiveType.Sphere, "Garden Flower", center + offset + Vector3.up * 0.3f, new Vector3(0.18f, 0.18f, 0.18f), new Color(0.98f, 0.68f, 0.36f)), root);
            }
            return root;
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
            // The island is curved: embed the footing into it, with its top flush with the walls.
            Parent(CreatePrimitive(PrimitiveType.Cube, "Completed House Footing", center + new Vector3(0f, -0.25f, 0f), new Vector3(2.13f, 0.5f, 1.73f), new Color(0.58f, 0.55f, 0.48f)), root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Completed House Walls", center + new Vector3(0f, 0.75f, 0f), new Vector3(2.05f, 1.5f, 1.65f), wall), root);
            var roofObject = CreatePrimitive(PrimitiveType.Cube, "Completed House Roof", center + new Vector3(0f, 1.85f, 0f), new Vector3(2.45f, 0.52f, 2.05f), roof);
            roofObject.transform.rotation = Quaternion.Euler(0f, 0f, 8f);
            Parent(roofObject, root);
            Parent(CreatePrimitive(PrimitiveType.Cube, "Completed House Door", center + new Vector3(0f, 0.52f, -0.84f), new Vector3(0.42f, 0.8f, 0.08f), new Color(0.32f, 0.16f, 0.07f)), root);
            Parent(CreatePrimitive(PrimitiveType.Cylinder, "Completed House Chimney", center + new Vector3(0.65f, 2.35f, 0.2f), new Vector3(0.2f, 0.48f, 0.2f), new Color(0.42f, 0.35f, 0.31f)), root);
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

    // GUI coordinates keep drag hit testing aligned with the IMGUI construction controls.
    public sealed class IslandOrbitDrag
    {
        private bool dragging;
        public float Yaw { get; set; }
        public float Pitch { get; set; } = 30;

        public bool Handle(EventType type, Vector2 delta, bool overPanel)
        {
            if (type == EventType.MouseDown)
            {
                dragging = !overPanel;
                return dragging;
            }
            if (type == EventType.MouseUp || type == EventType.MouseLeaveWindow)
            {
                var wasDragging = dragging;
                Cancel();
                return wasDragging;
            }
            if (type != EventType.MouseDrag || !dragging) return false;
            Yaw = Mathf.Repeat(Yaw - delta.x * 0.35f, 360);
            Pitch = Mathf.Clamp(Pitch + delta.y * 0.25f, 15, 65);
            return true;
        }

        public void Cancel() => dragging = false;
    }
}
