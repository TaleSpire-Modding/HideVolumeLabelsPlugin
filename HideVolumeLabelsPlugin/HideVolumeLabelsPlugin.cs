using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using RadialUI;
using TMPro;
using Unity.Mathematics;
using BoardPersistence = HolloFoxes.BoardPersistence;

namespace HideVolumeLabelsPlugin
{

    [BepInPlugin(Guid, "HolloFoxes' Hide Volume Labels Plug-In", Version)]
    [BepInDependency(RadialUIPlugin.Guid)]
    [BepInDependency(HolloFoxes.BoardPersistence.Guid)]
    public class HideVolumeLabelsPlugin : BaseUnityPlugin
    {
        // constants
        public const string Guid = "org.hollofox.plugins.HideVolumeLabelsPlugin";
        public const string G = "L";
        private const string Version = "1.0.0.0";

        private static Dictionary<HideVolumeItem, Dictionary<string,string>> Labels
            = new Dictionary<HideVolumeItem, Dictionary<string, string>>();

        // Config
        private ConfigEntry<Color> baseColor { get; set; }
        private Dictionary<string, string> colorizations = new Dictionary<string, string>();
        private string dir = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/")) + "/TaleSpire_CustomData/";

        public static void SetLabel(HideVolumeItem volume, string key, string text)
        {
            if (!Labels.ContainsKey(volume))
            {
                var entry = new Dictionary<string, string>();
                Labels.Add(volume,entry);
            }
            if (!Labels[volume].ContainsKey(key)) Labels[volume].Add(key, text);
            else if (text == "") Labels[volume].Remove(key);
            else Labels[volume][key] = text;

            SaveLabels();
        }

        private static void SaveLabels()
        {
            var allVolumes = HideVolumeLabelsPlugin.allVolumes();

            // Convert HideVolumeItem to Index
            Dictionary<int, Dictionary<string, string>> toInt = new Dictionary<int, Dictionary<string, string>>();
            foreach (var key in Labels.Keys)
            {
                var index = allVolumes.IndexOf(key);
                toInt.Add(index,Labels[key]);
            }
            var info = JsonConvert.SerializeObject(toInt);

            HolloFoxes.BoardPersistence.SetInfo(G,info);
        }

        private static List<HideVolumeItem> allVolumes()
        {
            var a = HideVolumeManager.Instance;
            var hideVolumes = a.transform.GetChild(1).Children();
            var allVolumes = new List<HideVolumeItem>();
            for (int i = 0; i < hideVolumes.LongCount(); i++)
            {
                var volume = a.transform.GetChild(1).GetChild(i);
                var volumeComponent = volume.GetComponent<HideVolumeItem>();
                allVolumes.Add(volumeComponent);
            }

            return allVolumes;
        }

        private static bool LoadLabelsError = true;
        private static bool LoadLabels()
        {
            Debug.Log(BoardSessionManager.CurrentBoardInfo.Description);
            try
            {
                var allVolumes = HideVolumeLabelsPlugin.allVolumes();
                Labels.Clear();
                // Convert HideVolumeItem to Index
                Dictionary<int, Dictionary<string, string>> toInt =
                    JsonConvert.DeserializeObject<Dictionary<int, Dictionary<string, string>>>(
                        HolloFoxes.BoardPersistence.ReadInfo(G));
                foreach (var key in toInt.Keys)
                {
                    var volume = allVolumes[key];
                    Labels.Add(volume, toInt[key]);
                }

                return true;
            }
            catch (Exception e)
            {
                if (LoadLabelsError) Debug.LogError($"Labels not loaded: {e}");
                LoadLabelsError = false;
            }
            return false;
        }

        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            Logger.LogInfo("In Awake for HideVolumeLabels");

            Debug.Log("HideVolumeLabels Plug-in loaded");

            baseColor = Config.Bind("Appearance", "Base Text Color", UnityEngine.Color.black);

            if (File.Exists(dir + "Config/" + Guid + "/ColorizedKeywords.json"))
            {
                string json = File.ReadAllText(dir + "Config/" + Guid + "/ColorizedKeywords.json");
                colorizations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }

            ModdingTales.ModdingUtils.Initialize(this, Logger);

            // Register Group Menus in a branch
            RadialSubmenu.EnsureMainMenuItem(RadialUIPlugin.Guid + ".HideVolume.Labels",
                RadialSubmenu.MenuType.HideVolume,
                "Labels",
                sprite("Labels.png")
            );

            // Add Icons sub menu item
            RadialSubmenu.CreateSubMenuItem(RadialUIPlugin.Guid + ".HideVolume.Labels",
                "Set Hide Volume Name",
                sprite("Edit.png"),
                SetLabelName
            );

            HolloFoxes.BoardPersistence.Subscribe(G, HandleRequest);
        }

        private void SetLabelName(HideVolumeItem hideVolume, string guid,  MapMenuItem item)
        {
            hideVolume = RadialUIPlugin.GetLastRadialHideVolume();
            var current = "";
            if (Labels.ContainsKey(hideVolume) && Labels[hideVolume].ContainsKey(guid)) current = Labels[hideVolume][guid];
            SystemMessage.AskForTextInput("Set Hide Volume Label", "", "Set Label", SetLabel, ProcessLabel,"",null,current);
        }

        private static string Labelholder;
        private static bool processing;

        private void SetLabel(string input)
        {
            Labelholder = input;
            processing = true;
            ProcessLabel();
        }

        private void ProcessLabel()
        {
            if (processing)
            {
                var lastVolume = RadialUIPlugin.GetLastRadialHideVolume();
                SetLabel(lastVolume, G, Labelholder);
                processing = false;
            }
        }

        private static Sprite sprite(string FileName)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var path = dir + "\\" + FileName;
            return RadialSubmenu.GetIconFromFile(path);
        }

        public void HandleRequest(HolloFoxes.BoardPersistence.Change[] changes)
        {
            Debug.Log(changes);
            foreach (HolloFoxes.BoardPersistence.Change change in changes)
            {
                if (change.key == G)
                {
                    try
                    {
                        if (LoadLabels()) RenderLabels();
                    }
                    catch (Exception x)
                    {
                        Debug.Log("Exception: " + x);
                    }
                }
            }
        }

        public void RenderLabels()
        {
            var allVolumes = HideVolumeLabelsPlugin.allVolumes();

            // Convert HideVolumeItem to Index
            var deserialize = new Dictionary<int, Dictionary<string, string>>();
            foreach (var key in Labels.Keys)
            {
                var index = allVolumes.IndexOf(key);
                deserialize.Add(index, Labels[key]);
            }

            foreach (var key in deserialize.Keys)
            {
                var asset = allVolumes[key];

                if (asset != null)
                {
                    TextMeshPro creatureStateText = null;
                    var volume = asset.transform.GetComponent<BounceBoxCollider>();

                    var creatureBlock = asset.transform.FindChild(".LabelBlock")?.gameObject;
                    if (creatureBlock == null)
                    {
                        Debug.Log("Creating CreatureBlock GameObject");
                        creatureBlock = new GameObject(".LabelBlock");
                        creatureBlock.transform.position = asset.transform.position;
                        creatureBlock.transform.rotation = Quaternion.Euler(0, 0, 0);
                        creatureBlock.transform.SetParent(asset.transform);

                        Debug.Log("Creating TextMeshPro");
                        creatureStateText = creatureBlock.AddComponent<TextMeshPro>();
                        creatureStateText.transform.position = new Vector3(
                            creatureBlock.transform.position.x + (volume.Size.x / 2),
                            creatureBlock.transform.position.y + (volume.Size.y / 2),
                            creatureBlock.transform.position.z + (volume.Size.z / 2)
                        );
                        creatureStateText.transform.rotation = creatureBlock.transform.rotation;
                        creatureStateText.textStyle = TMP_Style.NormalStyle;
                        creatureStateText.enableWordWrapping = true;
                        creatureStateText.alignment = TextAlignmentOptions.Center;
                        creatureStateText.autoSizeTextContainer = true;
                        creatureStateText.color = baseColor.Value;
                        creatureStateText.fontSize = 5;
                        creatureStateText.fontWeight = FontWeight.Bold;
                    }
                    else
                    {
                        creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                    }
                    
                    creatureStateText.autoSizeTextContainer = false;
                    var deserial = deserialize[key];
                    string content = string.Join("\r\n", deserial.Select(d => d.Value));
                    if (colorizations.ContainsKey("<Default>"))
                    {
                        content = "<Default>" + content;
                    }

                    creatureStateText.richText = true;
                    foreach (KeyValuePair<string, string> replacement in colorizations)
                    {
                        content = content.Replace(replacement.Key, replacement.Value);
                    }

                    creatureStateText.text = content;
                    int lines = content.Split('\r').Length;

                    var result = recordedSizes[asset] - lastrecordedSizes[asset];

                    creatureStateText.transform.position = new Vector3(
                        creatureBlock.transform.position.x + (result.x / 2),
                        creatureBlock.transform.position.y + (result.y / 2), 
                        creatureBlock.transform.position.z + (result.z / 2)
                        );
                    creatureStateText.transform.eulerAngles = new Vector3(20, Camera.main.transform.eulerAngles.y, 0);
                    creatureStateText.autoSizeTextContainer = true;
                }
            }
        }

        private void ChangeLabelAngel()
        {
            var allVolumes = HideVolumeLabelsPlugin.allVolumes();

            // Convert HideVolumeItem to Index
            var deserialize = new Dictionary<int, Dictionary<string, string>>();
            foreach (var key in Labels.Keys)
            {
                var index = allVolumes.IndexOf(key);
                deserialize.Add(index, Labels[key]);
            }

            foreach (var key in deserialize.Keys)
            {
                var asset = allVolumes[key];

                if (asset != null)
                {
                    var creatureBlock = asset.transform.FindChild(".LabelBlock")?.gameObject;
                    var creatureStateText = creatureBlock.GetComponent<TextMeshPro>();
                    creatureStateText.transform.eulerAngles = new Vector3(20, Camera.main.transform.eulerAngles.y, 0);
                }
            }

            
        }

        private static bool last;
        private static bool second = true;

        /// <summary>
        /// Looping method run by plugin
        /// </summary>
        void Update()
        {
            if (OnBoard())
            {
                if (!last)
                {
                    if (!LoadLabels()) return;
                    LoadLabelsError = true;
                }
                else if (second)
                {
                    RenderLabels();
                    ChangeLabelAngel();
                    second = false;
                }
                
                if (sizeChange()) RenderLabels();
                if (angleChange()) ChangeLabelAngel();
                last = true;
            }
            else
            {
                if (last)
                {
                    Labels.Clear();
                }
                last = false;
                second = true;
            }
        }

        private float cameraY = 0;
        private bool angleChange()
        {
            if (cameraY == Camera.main.transform.eulerAngles.y) return false;
            cameraY = Camera.main.transform.eulerAngles.y;
            return true;
        }

        private Dictionary<HideVolumeItem, float3> lastrecordedSizes = new Dictionary<HideVolumeItem, float3>();
        private Dictionary<HideVolumeItem, float3> recordedSizes = new Dictionary<HideVolumeItem, float3>();

        private bool sizeChange()
        {
            
            bool changed = false;
            var allVolumes = HideVolumeLabelsPlugin.allVolumes();
            foreach (var asset in allVolumes)
            {
                var volume = asset.transform.GetComponent<BounceBoxCollider>();
                if (!lastrecordedSizes.ContainsKey(asset)) lastrecordedSizes.Add(asset, new float3());
                if (!recordedSizes.ContainsKey(asset))
                {
                    changed = true; // added
                    recordedSizes.Add(asset,volume.Size);
                }
                lastrecordedSizes[asset] = new float3(recordedSizes[asset]);

                if (!recordedSizes[asset].Equals(volume.Size))
                {
                    recordedSizes[asset] = volume.Size;
                    changed = true;
                }
            }
            return changed;
        }

        private bool OnBoard()
        {
            return (CameraController.HasInstance &&
                    BoardSessionManager.HasInstance &&
                    BoardSessionManager.HasBoardAndIsInNominalState &&
                    !BoardSessionManager.IsLoading);
        }
    }
}
