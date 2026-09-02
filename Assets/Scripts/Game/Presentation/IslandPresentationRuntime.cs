using System.Collections;
using UnityEngine;

namespace CloudWhale.Game.Presentation
{
    /// <summary>Creates the complete no-asset diorama and IMGUI overlay at runtime, so Main needs no manual wiring.</summary>
    public sealed class IslandPresentationRuntime : MonoBehaviour
    {
        private readonly HouseFoundationCost displayedFoundationCost = HouseFoundationCost.Zero;
        private IslandPresentationController presentation;
        private GameObject foundation;
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
            var production = FindFirstObjectByType<OpenGameProductionRuntime>();
            while (production == null || production.Session == null)
            {
                yield return null;
                production = FindFirstObjectByType<OpenGameProductionRuntime>();
            }

            presentation = new IslandPresentationController(production.Session, displayedFoundationCost);
            CreateDiorama();
            CreateStyles();
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
            if (presentation == null || panelStyle == null) return;
            var view = presentation.View;
            GUI.Box(new Rect(20, 20, 410, 252), GUIContent.none, panelStyle);
            GUI.Label(new Rect(38, 34, 370, 28), "Cloudwhale Island", textStyle);
            GUI.Label(new Rect(38, 70, 360, 92), ResourceText(view.Resources), textStyle);
            GUI.Label(new Rect(38, 166, 360, 26), "House: " + (view.HouseStage == HouseStage.Foundation ? "Foundation" : "Unbuilt"), textStyle);
            GUI.Label(new Rect(38, 192, 360, 42), view.NextAction, textStyle);
            if (view.HouseStage == HouseStage.Unbuilt && GUI.Button(new Rect(38, 238, 190, 38), "Build foundation", buttonStyle))
            {
                presentation.BuildFoundation();
                ApplyHouseStage();
            }

            GUI.Box(new Rect(20, Screen.height - 62, 560, 42), view.StatusMessage, panelStyle);
        }

        private static string ResourceText(ResourceAmounts r) =>
            "Driftwood  " + r.Driftwood + "\nCloud Cotton  " + r.CloudCotton + "\nDew  " + r.Dew + "\nStardust  " + r.Stardust;

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

            var whale = CreatePrimitive(PrimitiveType.Capsule, "Sleeping Sky Whale", new Vector3(0, -1.6f, 0.4f), new Vector3(4.8f, 1.45f, 7.8f), new Color(0.33f, 0.55f, 0.78f));
            whale.transform.rotation = Quaternion.Euler(0, 0, 90);
            CreatePrimitive(PrimitiveType.Sphere, "Island Meadow", new Vector3(0, 0.1f, 0), new Vector3(7.8f, 1.25f, 5.4f), new Color(0.37f, 0.68f, 0.4f));
            CreatePrimitive(PrimitiveType.Cylinder, "House Place", new Vector3(0.65f, 0.76f, 0.2f), new Vector3(2.2f, 0.16f, 2.2f), new Color(0.81f, 0.64f, 0.42f));
            CreatePrimitive(PrimitiveType.Sphere, "Cloud One", new Vector3(-5, 4, 4), new Vector3(2.2f, 0.8f, 1.2f), Color.white);
            CreatePrimitive(PrimitiveType.Sphere, "Cloud Two", new Vector3(5, 3, 5), new Vector3(1.8f, 0.65f, 1.1f), Color.white);
            CreatePrimitive(PrimitiveType.Cylinder, "Tree", new Vector3(-2.1f, 1.35f, 0.3f), new Vector3(0.3f, 1.1f, 0.3f), new Color(0.36f, 0.21f, 0.1f));
            CreatePrimitive(PrimitiveType.Sphere, "Tree Canopy", new Vector3(-2.1f, 2.3f, 0.3f), new Vector3(1.25f, 1.15f, 1.25f), new Color(0.22f, 0.5f, 0.28f));
            foundation = CreatePrimitive(PrimitiveType.Cube, "House Foundation", new Vector3(0.65f, 1.03f, 0.2f), new Vector3(1.8f, 0.55f, 1.5f), new Color(0.7f, 0.51f, 0.36f));
        }

        private void ApplyHouseStage()
        {
            if (foundation != null && presentation != null) foundation.SetActive(presentation.View.HouseStage == HouseStage.Foundation);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name; item.transform.position = position; item.transform.localScale = scale;
            var material = new Material(Shader.Find("Standard")) { color = color };
            item.GetComponent<Renderer>().material = material;
            return item;
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
