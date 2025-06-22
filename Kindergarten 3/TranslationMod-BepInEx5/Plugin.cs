using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TextFx;
using TMPro;
using UnityEngine;

namespace TranslationMod_BepInEx5 {
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        private const string CrayawnSDF = "Crayawn SDF";
        private const string ChunkfiveExSDF = "Chunkfive Ex SDF";
        private const string MonstermonDescriptionTextGameObjectName = "MonstermonDescription";

        private new static ManualLogSource Logger;

        private static Dictionary<string, string> _localizationDictionary;
        private static readonly Dictionary<string, TMP_FontAsset> _fontAsset = new();

        private void Awake() {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable"), HarmonyPostfix]
        public static void PostOnEnable(TextMeshProUGUI __instance) {
            if (string.IsNullOrWhiteSpace(__instance.text)) return;
            Logger.LogDebug("OnEnable called. Replacing TextMeshProUGUI text.");

            if (__instance.name == "VersionNumber" && !__instance.text.Contains("Zarpyk")) {
                __instance.text += "<br>Mod creado por: Zarpyk";
            }
            
            LocalizeText(__instance, __instance.text);
        }

        // Postfix TMP_Text.text setter
        [HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter), HarmonyPostfix]
        public static void PostSetText(TMP_Text __instance, string value) {
            if (string.IsNullOrWhiteSpace(value)) return;

            Logger.LogDebug($"Checking if auto-sizing is needed for {__instance.name}");
            switch (__instance.name) {
                case "SubtitleText":
                case "Title" when __instance.text == "Options":
                case "File #1" when __instance.text == "File #1":
                case "File #2" when __instance.text == "File #2":
                case "File #3" when __instance.text == "File #3":
                case "Eat Apple" when __instance.text == "Eat Apple": __instance.enableAutoSizing = true; break;
            }
            switch (__instance.text) {
                case "Before School":
                case "Home":
                case "Morning Time":
                case "Lunch Time":
                case "Recess":
                case "After School":
                case "At Home":
                case "Nugget Cave":
                case "The End": __instance.enableAutoSizing = true; break;
            }

            Logger.LogDebug($"Setting text for {__instance.name} to '{value}'");
            LocalizeText(__instance, value);
        }

        /*[HarmonyPatch(typeof(SaveLoadFile), "LoadGame", typeof(int)), HarmonyPrefix]
        public static bool UnlockAllLoadGame(ref SaveData __result, int x) {
            SaveData saveData = new(x);
            for (int i = 0; i < saveData.unlocks.Length; i++) {
                saveData.unlocks[i] = true;
            }
            for (int i = 0; i < saveData.monstermon.Length; i++) {
                saveData.monstermon[i] = true;
                saveData.newMonstermon[i] = true;
            }
            __result = saveData;
            return false;
        }*/

        private static void LocalizeText(TMP_Text __instance, string value) {
            Logger.LogDebug($"Localizing text: '{value}' in {__instance.name}");
            Dictionary<string, string> localizationDictionary = GetLocalizationDictionary();
            Logger.LogDebug($"Localization dictionary contains {localizationDictionary?.Count ?? 0} entries.");
            TMP_FontAsset fontAsset = GetFont(CrayawnSDF);
            Logger.LogDebug($"Font asset loaded: {fontAsset?.name ?? "null"}");
            if (localizationDictionary == null || fontAsset == null) {
                Logger.LogError("Localization dictionary or font asset is null. Cannot proceed with text replacement.");
                return;
            }

            Logger.LogDebug("Try process MonstermonDescription");
            if (__instance.gameObject.name == MonstermonDescriptionTextGameObjectName) {
                Logger.LogDebug("Processing MonstermonDescription text.");
                ProcessMonstermonDescription(__instance, value, localizationDictionary);
                return;
            }
            Logger.LogDebug("Processing regular text.");

            if (__instance.font.name == CrayawnSDF) {
                Logger.LogDebug($"Replacing font for {__instance.name} from {CrayawnSDF} to {fontAsset.name}");
                __instance.font = fontAsset;
            }

            if (__instance.name == "AgainText" && __instance.text.Contains("again")) {
                __instance.text = __instance.text.Replace("again", "otra vez");
                Logger.LogDebug($"Replaced 'again' with 'otra vez' in {__instance.name} text.");
                return;
            }

            Logger.LogDebug($"Text before processing: '{value}'");
            // Remove kgwait tags if they exist
            Regex startKgWaitRegex = new(@"^(<kgwait=\d+(\.\d+)?>)");
            Regex endKgWaitRegex = new(@"(<kgwait=\d+(\.\d+)?>)$");
            string startKgWait = string.Empty;
            string endKgWait = string.Empty;

            string textWithoutTags = value;
            Match startMatch = startKgWaitRegex.Match(value);
            if (startMatch.Success) {
                startKgWait = startMatch.Value;
                textWithoutTags = textWithoutTags.Substring(startMatch.Length);
            }

            Match endMatch = endKgWaitRegex.Match(textWithoutTags.TrimEnd());
            if (endMatch.Success) {
                endKgWait = endMatch.Value;
                textWithoutTags = textWithoutTags.Substring(0, textWithoutTags.Length - endMatch.Length);
            }

            textWithoutTags = textWithoutTags.Trim();
            Logger.LogDebug($"Text without kgwait tags: '{textWithoutTags}'");

            if (localizationDictionary.TryGetValue(value, out string localizedText)) {
                if (localizedText == value || localizedText == textWithoutTags) {
                    Logger.LogDebug($"Text '{value}' is already localized. No changes made.");
                    return;
                }
                __instance.text = localizedText;
                Logger.LogInfo($"Replaced text '{value}' with '{localizedText}'");
            } else {
                if (localizationDictionary.TryGetValue(textWithoutTags, out localizedText)) {
                    if (localizedText == value || localizedText == textWithoutTags) {
                        Logger.LogDebug($"Text '{value}' is already localized. No changes made.");
                        return;
                    }
                    string finalText = localizedText;
                    // Reapply kgwait tags if they were present
                    if (!string.IsNullOrEmpty(startKgWait) || !string.IsNullOrEmpty(endKgWait)) {
                        finalText = startKgWait + localizedText + endKgWait;
                    }
                    __instance.text = finalText;
                    Logger.LogInfo($"Replaced text '{value}' with '{finalText}' (preserving tags) in TextMeshProUGUI.");
                } else {
                    Logger.LogWarning($"No localization found for key: {value} (or its text without tags: '{textWithoutTags}')");
                }
            }
        }

        private static void ProcessMonstermonDescription(TMP_Text __instance, string value,
                                                         Dictionary<string, string> localizationDictionary) {
            if (__instance.font.name == ChunkfiveExSDF) __instance.font = GetFont(ChunkfiveExSDF);

            Regex brRegex = new("<br>", RegexOptions.IgnoreCase);
            string[] parts = brRegex.Split(value);
            if (parts.Length != 2) {
                Logger.LogWarning($"Unexpected Monstermon description format: {value}");
                return;
            }

            string title = parts[0].Trim();
            string description = parts[1].Trim();

            string localizedTitle = title;
            string localizedDescription = description;

            bool isTitleLocalized = false;
            if (localizationDictionary.TryGetValue(title, out string translatedTitle)) {
                if (translatedTitle == title) {
                    Logger.LogDebug($"Title '{title}' is already localized. No changes made.");
                } else {
                    localizedTitle = translatedTitle;
                    isTitleLocalized = true;
                }
            }
            if (localizationDictionary.TryGetValue(description, out string translatedDescription)) {
                if (translatedDescription == description) {
                    Logger.LogDebug($"Description '{description}' is already localized. No changes made.");
                } else {
                    localizedDescription = translatedDescription;
                    isTitleLocalized = true;
                }
            }

            if (isTitleLocalized) {
                __instance.text = $"{localizedTitle}<br>{localizedDescription}";
                Logger.LogInfo($"Processed Monstermon description: '{__instance.text}'");
            }
        }

        private static Dictionary<string, string> GetLocalizationDictionary() {
            if (_localizationDictionary != null) return _localizationDictionary;

            string parentPath = Path.GetDirectoryName(Application.dataPath); // Game root folder
            if (parentPath == null) {
                Logger.LogError("Parent path is null");
                return null;
            }

            string dialogueBundlePath = Path.Combine(parentPath, "csv", "KG3Text.csv");
            if (!File.Exists(dialogueBundlePath)) {
                Logger.LogError("KG3Text.csv not found.");
                return null;
            }
            Dictionary<string, string> localizationDict = new();

            Regex csvParser = new(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
            string[] lines = File.ReadAllLines(dialogueBundlePath, Encoding.UTF8);
            foreach (string line in lines) {
                if (string.IsNullOrWhiteSpace(line)) {
                    Logger.LogWarning("Skipping empty line.");
                    continue;
                }
                string[] fields = csvParser.Split(line);
                if (fields.Length < 2) {
                    Logger.LogWarning($"Skipping line due to insufficient parts: {line}");
                    continue;
                }
                string key = fields[0].Trim('"').Trim().Replace("\"\"", "\"");
                string value = fields[1].Trim('"').Trim().Replace("\"\"", "\"");
                if (localizationDict.ContainsKey(key)) {
                    // Logger.LogInfo($"Duplicate key found: {key}.");
                    continue;
                }
                localizationDict[key] = value;
            }
            _localizationDictionary = localizationDict;
            Logger.LogInfo("Localization dictionary loaded successfully.");
            return localizationDict;
        }

        private static TMP_FontAsset GetFont(string name) {
            if (_fontAsset.TryGetValue(name, out TMP_FontAsset font)) return font;
            string parentPath = Path.GetDirectoryName(Application.dataPath);
            if (parentPath == null) {
                Logger.LogError("Parent path is null");
                return null;
            }
            IEnumerable<AssetBundle> allLoadedAssetBundles = AssetBundle.GetAllLoadedAssetBundles();
            AssetBundle fontBundle = null;
            foreach (AssetBundle assetBundle in allLoadedAssetBundles) {
                Logger.LogInfo(assetBundle.name);
                if (assetBundle.name == "font") {
                    fontBundle = assetBundle;
                    break;
                }
            }
            if (fontBundle == null) fontBundle = AssetBundle.LoadFromFile(Path.Combine(parentPath, "assets", "font"));
            if (fontBundle == null) {
                Logger.LogError("Failed to load AssetBundle!");
                return null;
            }
            _fontAsset[name] = fontBundle.LoadAsset<TMP_FontAsset>(name);
            return _fontAsset[name];
        }
    }
}