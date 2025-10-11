using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class GUIWizard : EditorWindow
{
    private enum ControlType { Label, Button, Toggle, Slider, TextField, TextArea }

    [System.Serializable]
    private class ControlData
    {
        public ControlType type;
        public string text = "New Control";
        public string variableName = "control";
        public Color color = Color.white;
        public Vector2 position = new Vector2(10, 10);
        public Vector2 size = new Vector2(100, 30);

        public bool toggleValue = false;
        public float sliderValue = 0.5f;
        public float sliderMin = 0f;
        public float sliderMax = 1f;
        public string textFieldValue = "";
        public bool isExpanded = true;
    }

    [System.Serializable]
    private class WindowData
    {
        public string name = "New Window";
        public List<ControlData> controls = new List<ControlData>();
        public Vector2 scrollPos;
        public bool isExpanded = true;
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        public Vector2 windowPosition = new Vector2(50, 50);
        public Vector2 windowSize = new Vector2(300, 200);
    }

    private List<WindowData> windows = new List<WindowData>();
    private Vector2 leftPanelScrollPos;
    private int selectedWindowIndex = 0;
    private string projectName = "Basic Project";
    private bool showHelp = false;
    private bool showSettings = false;

    [MenuItem("Tools/GUI-Wizard")]
    public static void ShowWindow()
    {
        var window = GetWindow<GUIWizard>("GUI-Wizard");
        window.minSize = new Vector2(800, 600);
        if (window.windows.Count == 0)
        {
            window.windows.Add(new WindowData());
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        float splitWidth = position.width * 0.45f;

        Rect leftRect = new Rect(5, 40, splitWidth - 10, position.height - 45);
        GUILayout.BeginArea(leftRect);
        DrawLeftPanel();
        GUILayout.EndArea();

        Rect rightRect = new Rect(splitWidth + 10, 40, position.width - splitWidth - 15, position.height - 45);
        GUILayout.BeginArea(rightRect);
        DrawPreviewPanel();
        GUILayout.EndArea();

        EditorGUI.DrawRect(new Rect(splitWidth + 5, 40, 1, position.height - 40), new Color(0.5f, 0.5f, 0.5f));
    }

    private void DrawTopToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        projectName = EditorGUILayout.TextField("Project:", projectName, GUILayout.Width(500));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("New Project", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            NewProject();
        }

        if (GUILayout.Button("Load Code", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            LoadGUICode();
        }

        if (GUILayout.Button("Save Project", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SaveProject();
        }

        if (GUILayout.Button("Load Project", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            LoadProject();
        }

        showSettings = GUILayout.Toggle(showSettings, "Settings", EditorStyles.toolbarButton, GUILayout.Width(60));

        showHelp = GUILayout.Toggle(showHelp, "Help", EditorStyles.toolbarButton, GUILayout.Width(40));

        GUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        if (showHelp)
        {
            DrawHelpPanel();
            return;
        }

        leftPanelScrollPos = EditorGUILayout.BeginScrollView(leftPanelScrollPos);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Windows", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+", GUILayout.Width(25)))
        {
            windows.Add(new WindowData { name = $"Window {windows.Count + 1}" });
        }
        GUILayout.EndHorizontal();


        if (windows.Count > 1)
        {
            string[] windowNames = windows.Select(w => w.name).ToArray();
            selectedWindowIndex = GUILayout.Toolbar(selectedWindowIndex, windowNames);
        }

        if (windows.Count == 0)
        {
            EditorGUILayout.HelpBox("No windows created. Click + to add a window.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        selectedWindowIndex = Mathf.Clamp(selectedWindowIndex, 0, windows.Count - 1);
        var selectedWindow = windows[selectedWindowIndex];

        GUILayout.Space(10);

        GUILayout.BeginVertical("box");
        GUILayout.Label($"Window Settings: {selectedWindow.name}", EditorStyles.boldLabel);
        selectedWindow.name = EditorGUILayout.TextField("Window Name", selectedWindow.name);
        selectedWindow.backgroundColor = EditorGUILayout.ColorField("Background Color", selectedWindow.backgroundColor);
        selectedWindow.windowPosition = EditorGUILayout.Vector2Field("Window Position", selectedWindow.windowPosition);
        selectedWindow.windowSize = EditorGUILayout.Vector2Field("Window Size", selectedWindow.windowSize);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Duplicate Window"))
        {
            var newWindow = JsonUtility.FromJson<WindowData>(JsonUtility.ToJson(selectedWindow));
            newWindow.name += " Copy";
            windows.Add(newWindow);
        }
        if (windows.Count > 1 && GUILayout.Button("Delete Window"))
        {
            if (EditorUtility.DisplayDialog("Delete Window", $"Are you sure you want to delete '{selectedWindow.name}'?", "Yes", "No"))
            {
                windows.RemoveAt(selectedWindowIndex);
                selectedWindowIndex = Mathf.Max(0, selectedWindowIndex - 1);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.Label($"Controls in {selectedWindow.name}", EditorStyles.boldLabel);

        for (int i = 0; i < selectedWindow.controls.Count; i++)
        {
            var control = selectedWindow.controls[i];

            GUILayout.BeginVertical("box");

            GUILayout.BeginHorizontal();
            control.isExpanded = EditorGUILayout.Foldout(control.isExpanded, $"{control.type}: {control.text}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                selectedWindow.controls.RemoveAt(i);
                break;
            }
            GUILayout.EndHorizontal();

            if (control.isExpanded)
            {
                control.type = (ControlType)EditorGUILayout.EnumPopup("Type", control.type);
                control.text = EditorGUILayout.TextField("Text/Label", control.text);
                control.variableName = EditorGUILayout.TextField("Variable Name", control.variableName);
                control.color = EditorGUILayout.ColorField("Color", control.color);
                control.position = EditorGUILayout.Vector2Field("Position", control.position);
                control.size = EditorGUILayout.Vector2Field("Size", control.size);

                switch (control.type)
                {
                    case ControlType.Toggle:
                        control.toggleValue = EditorGUILayout.Toggle("Default Value", control.toggleValue);
                        break;
                    case ControlType.Slider:
                        control.sliderValue = EditorGUILayout.Slider("Default Value", control.sliderValue, control.sliderMin, control.sliderMax);
                        control.sliderMin = EditorGUILayout.FloatField("Min Value", control.sliderMin);
                        control.sliderMax = EditorGUILayout.FloatField("Max Value", control.sliderMax);
                        break;
                    case ControlType.TextField:
                    case ControlType.TextArea:
                        control.textFieldValue = EditorGUILayout.TextField("Default Text", control.textFieldValue);
                        break;
                }
            }

            GUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Add Control"))
        {
            selectedWindow.controls.Add(new ControlData { variableName = $"control{selectedWindow.controls.Count + 1}" });
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        if (GUILayout.Button("Copy Generated Code", GUILayout.Height(30)))
        {
            string code = GenerateCode();
            EditorGUIUtility.systemCopyBuffer = code;
            ShowNotification(new GUIContent("Code copied to clipboard!"));
        }
    }

    private void DrawPreviewPanel()
    {
        GUILayout.Label("Live Preview", EditorStyles.boldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        previewRect.height = Mathf.Max(previewRect.height, 400);

        EditorGUI.DrawRect(previewRect, new Color(0.1f, 0.1f, 0.1f));

        if (Event.current.type == EventType.Repaint)
        {
            GUI.BeginGroup(previewRect);

            foreach (var window in windows)
            {
                Rect windowRect = new Rect(window.windowPosition, window.windowSize);
                GUI.color = window.backgroundColor;
                GUI.Box(windowRect, "");
                GUI.color = Color.white;

                GUI.Label(new Rect(windowRect.x + 5, windowRect.y + 5, windowRect.width - 10, 20), window.name, EditorStyles.boldLabel);

                GUI.BeginGroup(windowRect);
                foreach (var control in window.controls)
                {
                    GUI.color = control.color;
                    Rect controlRect = new Rect(control.position, control.size);

                    controlRect.y += 25;

                    if (controlRect.xMax <= windowRect.width && controlRect.yMax <= windowRect.height)
                    {
                        switch (control.type)
                        {
                            case ControlType.Label:
                                GUI.Label(controlRect, control.text);
                                break;
                            case ControlType.Button:
                                GUI.Button(controlRect, control.text);
                                break;
                            case ControlType.Toggle:
                                GUI.Toggle(controlRect, control.toggleValue, control.text);
                                break;
                            case ControlType.Slider:
                                GUI.HorizontalSlider(controlRect, control.sliderValue, control.sliderMin, control.sliderMax);
                                break;
                            case ControlType.TextField:
                                GUI.TextField(controlRect, control.textFieldValue);
                                break;
                            case ControlType.TextArea:
                                GUI.TextArea(controlRect, control.textFieldValue);
                                break;
                        }
                    }
                    GUI.color = Color.white;
                }
                GUI.EndGroup();
            }

            GUI.EndGroup();
        }
    }

    private void DrawHelpPanel()
    {
        GUILayout.Label("GUI-Wizard - Help", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox("Welcome to GUI-Wizard! Here's how to use it:", MessageType.Info);

        GUILayout.Label("Getting Started:", EditorStyles.boldLabel);
        GUILayout.Label("• Create windows to organize your GUI elements");
        GUILayout.Label("• Add controls to each window");
        GUILayout.Label("• See live preview on the right");
        GUILayout.Label("• Copy generated code to clipboard");

        GUILayout.Space(10);
        GUILayout.Label("Features:", EditorStyles.boldLabel);
        GUILayout.Label("• Multiple windows with separate controls");
        GUILayout.Label("• Load existing OnGUI code to continue editing");
        GUILayout.Label("• Save/Load projects as JSON files");
        GUILayout.Label("• Live preview with window backgrounds");
        GUILayout.Label("• Support for Labels, Buttons, Toggles, Sliders, TextFields");

        GUILayout.Space(10);
        GUILayout.Label("Tips:", EditorStyles.boldLabel);
        GUILayout.Label("• Use meaningful variable names for your controls");
        GUILayout.Label("• Position controls relative to their window");
        GUILayout.Label("• Save your project to preserve your work");
        GUILayout.Label("• Load Code can parse simple OnGUI methods");
    }

    private void NewProject()
    {
        if (EditorUtility.DisplayDialog("New Project", "This will clear all current windows. Continue?", "Yes", "Cancel"))
        {
            windows.Clear();
            windows.Add(new WindowData());
            selectedWindowIndex = 0;
            projectName = "MyGUIProject";
        }
    }

    private void SaveProject()
    {
        string path = EditorUtility.SaveFilePanel("Save OnGUI Project", "", projectName + ".json", "json");
        if (!string.IsNullOrEmpty(path))
        {
            var projectData = new { projectName = this.projectName, windows = this.windows };
            File.WriteAllText(path, JsonUtility.ToJson(projectData, true));
            ShowNotification(new GUIContent("Project saved!"));
        }
    }

    private void LoadProject()
    {
        string path = EditorUtility.OpenFilePanel("Load OnGUI Project", "", "json");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var projectData = JsonUtility.FromJson<ProjectData>(json);
                this.projectName = projectData.projectName;
                this.windows = projectData.windows ?? new List<WindowData>();
                if (windows.Count == 0) windows.Add(new WindowData());
                selectedWindowIndex = 0;
                ShowNotification(new GUIContent("Project loaded!"));
            }
            catch
            {
                EditorUtility.DisplayDialog("Error", "Failed to load project file.", "OK");
            }
        }
    }

    private void LoadGUICode()
    {
        string path = EditorUtility.OpenFilePanel("Load OnGUI Code", "", "cs");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            string code = File.ReadAllText(path);
            ParseGUICode(code);
        }
    }

    private void ParseGUICode(string code)
    {
        var window = new WindowData { name = "Imported Code" };

        // 1) Parse window title if it exists
        var windowMatch = Regex.Match(code, @"GUI\.Window\s*\(\s*\d+\s*,\s*new\s+Rect\s*\([^)]+\)\s*,\s*\w+\s*,\s*""([^""]+)""");
        if (windowMatch.Success)
        {
            window.name = windowMatch.Groups[1].Value;
        }

        // 2) Parse Labels (GUI.Label, GUILayout.Label, EditorGUILayout.LabelField)
        var labelMatches = Regex.Matches(code,
            @"(?:GUI|GUILayout|EditorGUILayout)\.(?:Label|LabelField)\s*\(\s*(?:new\s+Rect\s*\(\s*([^)]+)\)\s*,\s*)?""([^""]+)""");

        foreach (Match match in labelMatches)
        {
            // If there's no rect, default to (0,0) position & size
            Vector2 pos = Vector2.zero, size = new Vector2(100, 20);

            if (match.Groups[1].Success && TryParseRect(match.Groups[1].Value, out Vector2 parsedPos, out Vector2 parsedSize))
            {
                pos = parsedPos;
                size = parsedSize;
            }

            window.controls.Add(new ControlData
            {
                type = ControlType.Label,
                text = match.Groups[2].Value,
                position = pos,
                size = size
            });
        }

        // 3) Parse Buttons (GUI.Button and GUILayout.Button)
        var buttonMatches = Regex.Matches(code,
            @"(?:GUI|GUILayout)\.Button\s*\(\s*(?:new\s+Rect\s*\(\s*([^)]+)\)\s*,\s*)?""([^""]+)""");

        foreach (Match match in buttonMatches)
        {
            Vector2 pos = Vector2.zero, size = new Vector2(100, 20);

            if (match.Groups[1].Success && TryParseRect(match.Groups[1].Value, out Vector2 parsedPos, out Vector2 parsedSize))
            {
                pos = parsedPos;
                size = parsedSize;
            }

            window.controls.Add(new ControlData
            {
                type = ControlType.Button,
                text = match.Groups[2].Value,
                position = pos,
                size = size
            });
        }

        // 4) Add to list if any controls were found
        if (window.controls.Count > 0)
        {
            windows.Add(window);
            selectedWindowIndex = windows.Count - 1;
            ShowNotification(new GUIContent($"Imported {window.controls.Count} controls!"));
        }
        else
        {
            EditorUtility.DisplayDialog("Import Result", "No compatible GUI elements found in the code.", "OK");
        }
    }

    private bool TryParseRect(string rectParams, out Vector2 position, out Vector2 size)
    {
        position = Vector2.zero;
        size = Vector2.zero;

        var parts = rectParams.Split(',');
        if (parts.Length >= 4)
        {
            if (float.TryParse(parts[0].Trim().TrimEnd('f'), out float x) &&
                float.TryParse(parts[1].Trim().TrimEnd('f'), out float y) &&
                float.TryParse(parts[2].Trim().TrimEnd('f'), out float w) &&
                float.TryParse(parts[3].Trim().TrimEnd('f'), out float h))
            {
                position = new Vector2(x, y);
                size = new Vector2(w, h);
                return true;
            }
        }
        return false;
    }

    private string GenerateCode()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// Generated by GUI-Wizard");
        sb.AppendLine("// Project: " + projectName);
        sb.AppendLine();

        foreach (var window in windows)
        {
            foreach (var control in window.controls)
            {
                switch (control.type)
                {
                    case ControlType.Toggle:
                        sb.AppendLine($"private bool {control.variableName} = {control.toggleValue.ToString().ToLower()};");
                        break;
                    case ControlType.Slider:
                        sb.AppendLine($"private float {control.variableName} = {control.sliderValue}f;");
                        break;
                    case ControlType.TextField:
                    case ControlType.TextArea:
                        sb.AppendLine($"private string {control.variableName} = \"{control.textFieldValue}\";");
                        break;
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("void OnGUI()");
        sb.AppendLine("{");

        foreach (var window in windows)
        {
            sb.AppendLine($"    // Window: {window.name}");
            sb.AppendLine($"    GUI.color = new Color({window.backgroundColor.r}f, {window.backgroundColor.g}f, {window.backgroundColor.b}f, {window.backgroundColor.a}f);");
            sb.AppendLine($"    GUI.Box(new Rect({window.windowPosition.x}f, {window.windowPosition.y}f, {window.windowSize.x}f, {window.windowSize.y}f), \"{window.name}\");");
            sb.AppendLine("    GUI.color = Color.white;");
            sb.AppendLine();

            foreach (var control in window.controls)
            {
                float adjustedY = window.windowPosition.y + control.position.y + 25;
                float adjustedX = window.windowPosition.x + control.position.x;
                string rect = $"new Rect({adjustedX}f, {adjustedY}f, {control.size.x}f, {control.size.y}f)";
                string colorLine = $"    GUI.color = new Color({control.color.r}f, {control.color.g}f, {control.color.b}f, {control.color.a}f);";

                sb.AppendLine(colorLine);

                switch (control.type)
                {
                    case ControlType.Label:
                        sb.AppendLine($"    GUI.Label({rect}, \"{control.text}\");");
                        break;
                    case ControlType.Button:
                        sb.AppendLine($"    if (GUI.Button({rect}, \"{control.text}\"))");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        // {control.text} button clicked");
                        sb.AppendLine("    }");
                        break;
                    case ControlType.Toggle:
                        sb.AppendLine($"    {control.variableName} = GUI.Toggle({rect}, {control.variableName}, \"{control.text}\");");
                        break;
                    case ControlType.Slider:
                        sb.AppendLine($"    {control.variableName} = GUI.HorizontalSlider({rect}, {control.variableName}, {control.sliderMin}f, {control.sliderMax}f);");
                        break;
                    case ControlType.TextField:
                        sb.AppendLine($"    {control.variableName} = GUI.TextField({rect}, {control.variableName});");
                        break;
                    case ControlType.TextArea:
                        sb.AppendLine($"    {control.variableName} = GUI.TextArea({rect}, {control.variableName});");
                        break;
                }
                sb.AppendLine("    GUI.color = Color.white;");
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    [System.Serializable]
    private class ProjectData
    {
        public string projectName;
        public List<WindowData> windows;
    }
}