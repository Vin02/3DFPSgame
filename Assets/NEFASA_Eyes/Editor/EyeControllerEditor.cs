using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NEFASA.Eyes.Editor
{
    /// <summary>Native serialized controls shared by all three NEFASA pipelines.</summary>
    [CustomEditor(typeof(EyeController)), CanEditMultipleObjects]
    public sealed class EyeControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("nefasa-eye-controls");
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string folder = Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
            var style = string.IsNullOrEmpty(folder) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(folder + "/EyeControllerEditor.uss");
            if (style) root.styleSheets.Add(style);
            root.Add(new HelpBox(
                "Beide Augen und alle Detailstufen gemeinsam einstellen. Änderungen wirken auch im Spiel.\n" +
                "Adjust both eyes and every LOD together. Changes also work during gameplay.",
                HelpBoxMessageType.Info));

            AddControl(root, "irisColor", "Augenfarbe / Iris color", "Hauptfarbe der Iris. / Main iris color.");
            AddControl(root, "innerColor", "Innenfarbe / Inner color", "Zweite Irisfarbe nahe der Pupille und in Pigmentbereichen. / Secondary iris color near the pupil and in pigment areas.");
            AddControl(root, "pupilRadius", "Pupille (mm) / Pupil (mm)", "Tatsächlicher Radius, nicht Durchmesser: 0,9–3,2 mm. / Actual radius, not diameter: 0.9–3.2 mm.");
            AddControl(root, "fiberDepth", "Fasertiefe / Fiber depth", "Stärke des feinen Irisreliefs. / Strength of the fine iris relief.");
            AddControl(root, "vessels", "Äderchen / Vessels", "Sichtbarkeit der Gefäße im Augenweiß. / Visibility of blood vessels in the sclera.");
            AddControl(root, "pigmentation", "Pigment / Pigmentation", "Anteil der zweiten Irisfarbe. / Amount of secondary iris pigmentation.");
            AddControl(root, "limbalRing", "Irisrand / Limbal ring", "Dunkelheit des äußeren Irisrands. / Darkness of the outer iris border.");
            AddControl(root, "gloss", "Glanz / Gloss", "Glätte der feuchten Augenoberfläche und Hornhaut. / Smoothness of the wet surface and cornea.");
            AddControl(root, "moisture", "Feuchtigkeit / Moisture", "Stärke des feuchten Oberflächen-Eindrucks. / Strength of the wet surface response.");
            AddControl(root, "showOpticNerve", "Sehnerv / Optic nerve", "Optionalen Sehnerv an beiden Augen ein- oder ausblenden. / Show or hide the optional optic nerve on both eyes.");

            var heading = new Label("Farbvorlagen / Color presets");
            heading.AddToClassList("nefasa-eye-presets-heading");
            root.Add(heading);
            var presets = new VisualElement();
            presets.AddToClassList("nefasa-eye-presets");
            AddPreset(presets, "Blau / Blue", new Color(.28f, .39f, .46f), new Color(.40f, .34f, .22f));
            AddPreset(presets, "Braun / Brown", new Color(.22f, .13f, .07f), new Color(.37f, .23f, .11f));
            AddPreset(presets, "Grün / Green", new Color(.28f, .33f, .20f), new Color(.44f, .33f, .15f));
            AddPreset(presets, "Grau / Gray", new Color(.37f, .39f, .40f), new Color(.38f, .32f, .24f));
            AddPreset(presets, "Hasel / Hazel", new Color(.34f, .28f, .17f), new Color(.43f, .29f, .12f));
            AddPreset(presets, "Bernstein / Amber", new Color(.40f, .27f, .10f), new Color(.48f, .32f, .12f));
            root.Add(presets);
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                foreach (Object item in targets) if (item) ((EyeController)item).ApplySettings();
            });
            return root;
        }

        private void AddControl(VisualElement root, string name, string label, string tooltip)
        {
            var property = serializedObject.FindProperty(name);
            if (property == null)
            {
                root.Add(new HelpBox("Fehlende Einstellung / Missing setting: " + name, HelpBoxMessageType.Error));
                return;
            }
            var field = new PropertyField(property, label) { name = "nefasa-" + name, tooltip = tooltip };
            field.AddToClassList("nefasa-eye-setting");
            root.Add(field);
        }

        private void AddPreset(VisualElement parent, string name, Color iris, Color inner)
        {
            parent.Add(new Button(() =>
            {
                serializedObject.Update();
                serializedObject.FindProperty("irisColor").colorValue = iris;
                serializedObject.FindProperty("innerColor").colorValue = inner;
                serializedObject.ApplyModifiedProperties();
                foreach (Object item in targets) if (item) ((EyeController)item).ApplySettings();
            }) { text = name, tooltip = "Ändert beide Irisfarben. / Changes both iris colors." });
        }
    }
}
