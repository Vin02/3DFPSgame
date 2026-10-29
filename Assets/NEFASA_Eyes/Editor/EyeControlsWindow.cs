using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NEFASA.Eyes.Editor
{
    /// <summary>Native, serialized controls for the selected eye prefab.</summary>
    public sealed class EyeControlsWindow : EditorWindow
    {
        private const string WindowTitle = "NEFASA - Augen / Eye Controls";
        [SerializeField] private EyeController controller;
        private UnityEditor.Editor controllerEditor;
        private ObjectField rootField;
        private VisualElement inspectorHost;

        [MenuItem("Tools/NEFASA Eyes/Augen einstellen")]
        public static void Open()
        {
            bool wasOpen = HasOpenInstances<EyeControlsWindow>();
            var window = GetWindow<EyeControlsWindow>(true, WindowTitle, true);
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(470, 650);
            if (!wasOpen)
                window.position = new Rect(window.position.x, window.position.y, 620, 760);
            window.ShowUtility();
            var selected = ResolveSelection();
            if (!selected) selected = window.controller;
            if (!selected) selected = FindSceneController();
            window.SetController(selected);
            if (window.controller && !ResolveSelection())
                Selection.activeGameObject = window.controller.gameObject;
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(470, 650);
            if (!controller) controller = ResolveSelection() ?? FindSceneController();
            // Remove first so entering Play mode without a domain reload cannot
            // register an event twice on the same surviving window instance.
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ReleaseInspector();
            rootField = null;
            inspectorHost = null;
        }

        public void CreateGUI()
        {
            ReleaseInspector();
            rootVisualElement.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "eyeControlsScroll" };
            // Native layout helper keeps the real inspector usable on a small
            // utility window; all fields retain the current Unity editor theme.
            scroll.StretchToParentSize();
            rootVisualElement.Add(scroll);
            scroll.Add(new HelpBox(
                "1. Augenobjekt auswählen / Select the eye object.\n" +
                "2. Farbe oder Vorlage wählen / Choose a color or preset.\n" +
                "3. Details und Sehnerv einstellen / Adjust details and optic nerve.",
                HelpBoxMessageType.Info));
            rootField = new ObjectField("Augenobjekt / Eye object")
            {
                name = "eyeRootObject",
                objectType = typeof(GameObject),
                allowSceneObjects = true
            };
            rootField.RegisterValueChangedCallback(change => SetController(ResolveController(change.newValue as GameObject)));
            scroll.Add(rootField);
            scroll.Add(new Button(() => SetController(ResolveSelection() ?? FindSceneController()))
            {
                text = "Aus Auswahl übernehmen / Use selection"
            });
            inspectorHost = new VisualElement { name = "eyeInspectorHost" };
            scroll.Add(inspectorHost);
            if (!controller) controller = ResolveSelection() ?? FindSceneController();
            BuildInspector();
        }

        private void SetController(EyeController next)
        {
            bool same = controller == next;
            controller = next;
            if (rootField != null) rootField.SetValueWithoutNotify(controller ? controller.gameObject : null);
            if (inspectorHost == null) return;
            if (same && controllerEditor && controllerEditor.target == controller) return;
            BuildInspector();
        }

        private void BuildInspector()
        {
            ReleaseInspector();
            if (inspectorHost == null) return;
            inspectorHost.Clear();
            rootField?.SetValueWithoutNotify(controller ? controller.gameObject : null);
            if (!controller)
            {
                inspectorHost.Add(new HelpBox(
                    "Kein EyeController gefunden. EyePair oder SingleEye in die Szene ziehen und hier auswählen.\n" +
                    "No EyeController found. Add EyePair or SingleEye to the scene and select it here.",
                    HelpBoxMessageType.Info));
                return;
            }
            controllerEditor = UnityEditor.Editor.CreateEditor(controller, typeof(EyeControllerEditor));
            controllerEditor.serializedObject.UpdateIfRequiredOrScript();
            VisualElement inspector = controllerEditor.CreateInspectorGUI();
            inspectorHost.Add(inspector);
            // This is an EditorWindow host, rather than Unity's InspectorWindow,
            // so bind the reused inspector tree explicitly. SerializedObject
            // binding preserves Undo, prefab overrides and preset transactions.
            inspector.Bind(controllerEditor.serializedObject);
        }

        private void ReleaseInspector()
        {
            inspectorHost?.Unbind();
            if (controllerEditor) DestroyImmediate(controllerEditor);
            controllerEditor = null;
        }

        private void OnSelectionChanged()
        {
            var selected = ResolveSelection();
            if (selected) SetController(selected);
        }

        private void OnUndoRedo()
        {
            if (!controller) { SetController(ResolveSelection() ?? FindSceneController()); return; }
            controllerEditor?.serializedObject.UpdateIfRequiredOrScript();
            controller.ApplySettings();
            Repaint();
        }

        private void OnHierarchyChanged()
        {
            if (!controller) SetController(ResolveSelection() ?? FindSceneController());
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode) return;
            if (!controller) controller = ResolveSelection() ?? FindSceneController();
            BuildInspector();
        }

        private static EyeController ResolveSelection() => ResolveController(Selection.activeGameObject);

        private static EyeController ResolveController(GameObject selected)
        {
            if (!selected || EditorUtility.IsPersistent(selected)) return null;
            var parent = selected.GetComponentInParent<EyeController>(true);
            return parent ? parent : selected.GetComponentInChildren<EyeController>(true);
        }

        private static EyeController FindSceneController()
        {
            var candidates = Object.FindObjectsByType<EyeController>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            Scene active = SceneManager.GetActiveScene();
            foreach (var candidate in candidates)
                if (candidate.gameObject.scene == active && candidate.gameObject.activeInHierarchy) return candidate;
            foreach (var candidate in candidates)
                if (candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded && candidate.gameObject.activeInHierarchy) return candidate;
            foreach (var candidate in candidates)
                if (candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded) return candidate;
            return null;
        }
    }
}

