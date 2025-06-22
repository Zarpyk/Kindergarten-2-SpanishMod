using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Logging;
using DialogueTreeKG2;
using HarmonyLib;
using KG2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TranslationMod_BepInEx5 {
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin {
        private static ManualLogSource Logger;

        private void Awake() {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(NPCBehavior), "AddConversation"), HarmonyPrefix]
        public static bool AddConversation(NPCBehavior __instance, Room r, ref Dictionary<Room, Dialogue> ___conversations,
                                           ref Dialogue ___dialogue) {
            Logger.LogInfo("Loading conversation");
            if (!___conversations.TryGetValue(r, out Dialogue conversation)) {
                // Get one level up from the Application.dataPath directory
                string parentPath = GetRunningDirectory();
                if (parentPath == null) {
                    Logger.LogError("Parent path is null");
                    // Execute the original method
                    return true;
                }
                string path = Path.Combine(parentPath, "xml", __instance.transform.name.ToLower(),
                                           __instance.transform.name + r + ".txt");
                if (!File.Exists(path)) {
                    Logger.LogError("File not found: " + path);
                    // Execute the original method
                    return true;
                }
                ___conversations.Add(r, Dialogue.LoadDialogue(path));
                ___dialogue = ___conversations[r];
            } else ___dialogue = conversation;
            // Don't execute the original method
            return false;
        }

        [HarmonyPatch(typeof(UIController), "Start"), HarmonyPostfix]
        public static void UIControllerStart(ref TextMeshProUGUI ___mDialogueText, ref OptionBehavior[] ___mOptions) {
            Logger.LogInfo($"{___mDialogueText.font.name}");
            string parentPath = GetRunningDirectory();
            if (parentPath == null) {
                Logger.LogError("Parent path is null");
                return;
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
                return;
            }
            TMP_FontAsset tmpFontAsset = fontBundle.LoadAsset<TMP_FontAsset>("Crayawn SDF");
            ___mDialogueText.font = tmpFontAsset;

            Font font = fontBundle.LoadAsset<Font>("Crayawn");
            foreach (OptionBehavior optionBehavior in ___mOptions) {
                optionBehavior.optionText.font = font;
            }
        }

        [HarmonyPatch(typeof(XMLManager), "Awake"), HarmonyPrefix]
        public static bool XMLManagerAwake(XMLManager __instance, ref Dictionary<Room, List<DialogueNode>> ___objectXMLs) {
            XMLManager.instance = __instance;
            ___objectXMLs = new Dictionary<Room, List<DialogueNode>>();
            // Get one level up from the Application.dataPath directory
            string parentPath = GetRunningDirectory();
            if (parentPath == null) {
                Logger.LogError("Parent path is null");
                // Execute the original method
                return true;
            }

            foreach (Room key in (Enum.GetValues(typeof(Room)) as Room[])!) {
                string path = Path.Combine(parentPath, "xml", "objects", key + ".txt");
                if (!File.Exists(path)) continue;
                Dialogue.Dialoguer dialoguer =
                    (Dialogue.Dialoguer) new XmlSerializer(typeof(Dialogue.Dialoguer)).Deserialize(new StreamReader(path));
                if (dialoguer is { Nodes: not null }) ___objectXMLs.Add(key, dialoguer.Nodes);
            }

            // Don't execute the original method
            return false;
        }

        [HarmonyPatch(typeof(MonstermonPanel), "SerializeXML"), HarmonyPrefix]
        public static bool SerializeXML(ref MonstermonPanel.MonstermonPanelData ___mMonstermonPanelData,
                                        ref MonstermonList ___mMonstermonData, ref TextAsset ___monstermonPanelDataXML,
                                        ref Text ___deckText, ref Text ___backText) {
            ___mMonstermonPanelData = new MonstermonPanel.MonstermonPanelData();
            ___mMonstermonPanelData = ___mMonstermonPanelData.LoadMonstermonPanelData(___monstermonPanelDataXML);
            ___deckText.text = ___mMonstermonPanelData.DeckText;
            ___backText.text = ___mMonstermonPanelData.BackText;
            XmlSerializer xmlSerializer = new(typeof(MonstermonList));

            if (GetXML("MonstermonList.txt", out string path)) return true;

            StringReader stringReader = new(File.ReadAllText(path));
            ___mMonstermonData = (MonstermonList) xmlSerializer.Deserialize(stringReader);
            stringReader.Close();
            return false;
        }

        [HarmonyPatch(typeof(MonstermonUnlockPanel), "Start"), HarmonyPrefix]
        public static bool MonstermonUnlockPanelStart(MonstermonUnlockPanel __instance, ref RectTransform ___rect,
                                                      ref MonstermonList ___mMonstermonData) {
            if (GetXML("MonstermonList.txt", out string path)) return true;

            ___rect = __instance.GetComponent<RectTransform>();
            XmlSerializer xmlSerializer = new(typeof(MonstermonList));
            StringReader stringReader = new(File.ReadAllText(path));
            ___mMonstermonData = (MonstermonList) xmlSerializer.Deserialize(stringReader);
            stringReader.Close();
            return false;
        }

        [HarmonyPatch(typeof(MonstermonPanel.MonstermonPanelData), "LoadMonstermonPanelData"), HarmonyPrefix]
        public static bool LoadMonstermonPanelData(ref MonstermonPanel.MonstermonPanelData __result) {
            if (GetXML("MonstermonPanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(MonstermonPanel.MonstermonPanelData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            MonstermonPanel.MonstermonPanelData monstermonPanelData =
                (MonstermonPanel.MonstermonPanelData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = monstermonPanelData;
            return false;
        }

        [HarmonyPatch(typeof(DeathPanel.DeathMessages), "LoadDeathMessage"), HarmonyPrefix]
        public static bool LoadDeathMessage(ref DeathPanel.DeathMessages __result) {
            if (GetXML("DeathMessages.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(DeathPanel.DeathMessages));
            StringReader stringReader1 = new(File.ReadAllText(path));
            DeathPanel.DeathMessages deathMessages =
                (DeathPanel.DeathMessages) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = deathMessages;
            return false;
        }

        [HarmonyPatch(typeof(ClothingInfoList), "LoadClothingData"), HarmonyPrefix]
        public static bool LoadClothingData(ref ClothingInfoList __result) {
            if (GetXML("ClothingInfo.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(ClothingInfoList));
            StringReader stringReader1 = new(File.ReadAllText(path));
            ClothingInfoList clothingInfoList = (ClothingInfoList) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = clothingInfoList;
            return false;
        }

        [HarmonyPatch(typeof(AreYouSurePanel.AreYouSureData), "LoadAreYouSureData"), HarmonyPrefix]
        public static bool LoadAreYouSureData(ref AreYouSurePanel.AreYouSureData __result) {
            if (GetXML("AreYouSurePanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(AreYouSurePanel.AreYouSureData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            AreYouSurePanel.AreYouSureData areYouSureData =
                (AreYouSurePanel.AreYouSureData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = areYouSureData;
            return false;
        }

        [HarmonyPatch(typeof(EndDayPanel.EndDayPanelData), "LoadPanelData"), HarmonyPrefix]
        public static bool LoadPanelData(ref EndDayPanel.EndDayPanelData __result) {
            if (GetXML("EndDayPanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(EndDayPanel.EndDayPanelData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            EndDayPanel.EndDayPanelData endDayPanelData =
                (EndDayPanel.EndDayPanelData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = endDayPanelData;
            return false;
        }

        [HarmonyPatch(typeof(MissionMapPanel.MissionMapData), "LoadData"), HarmonyPrefix]
        public static bool LoadData(ref MissionMapPanel.MissionMapData __result) {
            if (GetXML("MissionMapPanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(MissionMapPanel.MissionMapData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            MissionMapPanel.MissionMapData missionMapData =
                (MissionMapPanel.MissionMapData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = missionMapData;
            return false;
        }

        [HarmonyPatch(typeof(PausePanel.PausePanelData), "LoadPausePanelData"), HarmonyPrefix]
        public static bool LoadPausePanelData(ref PausePanel.PausePanelData __result) {
            if (GetXML("PausePanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(PausePanel.PausePanelData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            PausePanel.PausePanelData pausePanelData =
                (PausePanel.PausePanelData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = pausePanelData;
            return false;
        }

        [HarmonyPatch(typeof(RestartFromOtherPanel.RestartData), "LoadRestartPanelData"), HarmonyPrefix]
        public static bool LoadRestartPanelData(ref RestartFromOtherPanel.RestartData __result) {
            if (GetXML("RestartPanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(RestartFromOtherPanel.RestartData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            RestartFromOtherPanel.RestartData restartData =
                (RestartFromOtherPanel.RestartData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = restartData;
            return false;
        }

        [HarmonyPatch(typeof(TitleScreenPanel.TitleScreenPanelData), "LoadTitleScreenPanelData"), HarmonyPrefix]
        public static bool LoadTitleScreenPanelData(ref TitleScreenPanel.TitleScreenPanelData __result) {
            if (GetXML("TitleScreenData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(TitleScreenPanel.TitleScreenPanelData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            TitleScreenPanel.TitleScreenPanelData titleScreenPanelData =
                (TitleScreenPanel.TitleScreenPanelData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = titleScreenPanelData;
            return false;
        }

        [HarmonyPatch(typeof(TuesdayPanel.TuesdayPanelData), "LoadTuesdayPanelData"), HarmonyPrefix]
        public static bool LoadTuesdayPanelData(ref TuesdayPanel.TuesdayPanelData __result) {
            if (GetXML("TuesdayPanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(TuesdayPanel.TuesdayPanelData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            TuesdayPanel.TuesdayPanelData tuesdayPanelData =
                (TuesdayPanel.TuesdayPanelData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = tuesdayPanelData;
            return false;
        }

        [HarmonyPatch(typeof(UnlockableItemsPanel.UnlockableItemsData), "LoadPanelData"), HarmonyPrefix]
        public static bool LoadPanelData(ref UnlockableItemsPanel.UnlockableItemsData __result) {
            if (GetXML("UnlockableItemsData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(UnlockableItemsPanel.UnlockableItemsData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            UnlockableItemsPanel.UnlockableItemsData unlockableItemsData =
                (UnlockableItemsPanel.UnlockableItemsData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = unlockableItemsData;
            return false;
        }

        [HarmonyPatch(typeof(WardrobePanel.WardrobePanelData), "LoadData"), HarmonyPrefix]
        public static bool LoadData(ref WardrobePanel.WardrobePanelData __result) {
            if (GetXML("WardrobePanelData.txt", out string path)) return true;

            XmlSerializer xmlSerializer = new(typeof(WardrobePanel.WardrobePanelData));
            StringReader stringReader1 = new(File.ReadAllText(path));
            WardrobePanel.WardrobePanelData wardrobePanelData =
                (WardrobePanel.WardrobePanelData) xmlSerializer.Deserialize(stringReader1);
            stringReader1.Close();
            __result = wardrobePanelData;
            return false;
        }

        private static bool GetXML(string fileName, out string path) {
            string parentPath = GetRunningDirectory();
            if (parentPath == null) {
                Logger.LogError("Parent path is null");
                path = "";
                // Execute the original method
                return true;
            }
            path = Path.Combine(parentPath, "xml", "menus-textassets", fileName);
            if (!File.Exists(path)) {
                Logger.LogError("File not found: " + path);
                // Execute the original method
                return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(Monstermon_GameManager), "GetTutorialText"), HarmonyPrefix]
        private static bool GetTutorialText(ref string __result, int a) {
            __result = a switch {
                1 =>
                    "¡Monstermon es un juego de cartas simple y rápido! Intenta derrotar a tu oponente reduciendo sus Puntos de Salud a 0.",
                2 => "Este es el nombre del Monstermon.",
                3 => "Este es el tipo del Monstermon. Esto no tiene ningún efecto en el juego, ¡es solo decorativo!",
                4 => "Esta es la Acción del Monstermon. Por 1 de Maná, ¡el Babosa Celestial causará 2 de daño a tu oponente!",
                5 =>
                    "Esta es la Reacción del Monstermon. Puedes jugar Reacciones cuando tu oponente juega un Monstermon en su turno. Las Reacciones también cuestan maná, ¡así que ten cuidado!",
                6 =>
                    "Estas esferas azules son tu Maná, que usas para jugar Acciones y Reacciones. Al inicio de tu turno, ganarás 1, pero solo puedes tener un máximo de 5.",
                7 =>
                    "Durante tu turno, puedes Pasar en lugar de jugar una carta para ganar 1 Maná adicional para un turno futuro.",
                8 =>
                    "¡Ahora es el momento de jugar! Hay muchos Monstermon con Acciones y Reacciones interesantes por descubrir. ¡Buena suerte!",
                _ => ""
            };
            return false;
        }

        [HarmonyPatch(typeof(Monstermon_GameManager), "GetTutorialConfirmText"), HarmonyPrefix]
        private static bool GetTutorialConfirmText(ref string __result, int a) {
            __result = a switch {
                1 => "Está bien.",
                2 => "Entendido.",
                3 => "¡Genial!",
                4 => "Está bien.",
                5 => "DE ACUERDO.",
                6 => "SÍ.",
                7 => "¡ENTIENDO!",
                8 => "¡JUGAR!",
                _ => ""
            };
            return false;
        }

        // It is written on the card sprite, so this is useless
        /*[HarmonyPatch(typeof(Monstermon_GameManager), "ResolveOpponentReaction"),
         HarmonyPatch(typeof(Monstermon_GameManager), "ResolvePlayerReaction"),
         HarmonyPostfix]
        public static void ResolveReaction(Monstermon_GameManager __instance, int ___totalBlock) {
            if (__instance.txtBlock.text == "Reflected!") {
                __instance.txtBlock.text = "Reflejado!";
            } else if (__instance.txtBlock.text == "Blocked!") {
                __instance.txtBlock.text = "Bloqueado!";
            } else if (__instance.txtBlock.text == "-" + ___totalBlock + " Damage!") {
                __instance.txtBlock.text = "-" + ___totalBlock + " de Daño!";
            }
        }

        [HarmonyPatch(typeof(Monstermon_GameManager), "GetMonstermon"),
         HarmonyPrefix]
        public static bool GetMonstermon(Monstermon_GameManager __instance, ref Monstermon __result, int id) {
            Monstermon monstermon = id switch {
                1 => new Monstermon(id, "Babosa Celestial", "Azul", "Mágico",
                                    GetAction(1), GetReaction(2)),
                2 => new Monstermon(id, "Moco Duro", "Azul", "Bestia",
                                    GetAction(13), GetReaction(1)),
                3 => new Monstermon(id, "Cubo de Agua", "Azul", "Mágico",
                                    GetAction(3), GetReaction(1)),
                4 => new Monstermon(id, "Atún Pálido", "Azul", "Pez",
                                    GetAction(1), GetReaction(2)),
                5 => new Monstermon(id, "Ultralodon", "Azul", "Pez",
                                    GetAction(14), GetReaction(0)),
                6 => new Monstermon(id, "Nimbo Carnívoro", "Azul", "Bestia",
                                    GetAction(13), GetReaction(1)),
                7 => new Monstermon(id, "Calamar Pequeño", "Azul", "Pez",
                                    GetAction(1), GetReaction(2)),
                8 => new Monstermon(id, "Coral Que Parece Mano", "Azul", "Forma Extraña",
                                    GetAction(9), GetReaction(7)),
                9 => new Monstermon(id, "Rana Ermitaña", "Azul", "Bestia",
                                    GetAction(7), GetReaction(16)),
                10 => new Monstermon(id, "Castillo de Arena", "Azul", "Artefacto",
                                     GetAction(15), GetReaction(16)),
                11 => new Monstermon(id, "Hombre en Llamas", "Rojo", "Desafortunado",
                                     GetAction(2), GetReaction(1)),
                12 => new Monstermon(id, "Silla de Púas", "Rojo", "Artefacto",
                                     GetAction(13), GetReaction(1)),
                13 => new Monstermon(id, "Cigaretmon", "Rojo", "Mágico",
                                     GetAction(1), GetReaction(4)),
                14 => new Monstermon(id, "Gusano de las Dunas", "Rojo", "Bestia",
                                     GetAction(14), GetReaction(0)),
                15 => new Monstermon(id, "Llama Estresada", "Rojo", "Bestia",
                                     GetAction(1), GetReaction(4)),
                16 => new Monstermon(id, "Patito Cíclope", "Rojo", "Bestia",
                                     GetAction(7), GetReaction(18)),
                17 => new Monstermon(id, "Zombie Adolescente Mutante", "Rojo", "Humanoide",
                                     GetAction(1), GetReaction(4)),
                18 => new Monstermon(id, "Dragón Solitario", "Rojo", "Bestia",
                                     GetAction(13), GetReaction(1)),
                19 => new Monstermon(id, "Hidra de Millones de Cabezas", "Rojo", "Bestia",
                                     GetAction(8), GetReaction(9)),
                20 => new Monstermon(id, "Héroe del Dab", "Rojo", "Humanoide",
                                     GetAction(15), GetReaction(18)),
                21 => new Monstermon(id, "Trampa Venenosa", "Verde", "Planta",
                                     GetAction(1), GetReaction(3)),
                22 => new Monstermon(id, "El Árbol Más Alto", "Verde", "Planta",
                                     GetAction(10), GetReaction(8)),
                23 => new Monstermon(id, "Tocón Relajado", "Verde", "Planta",
                                     GetAction(1), GetReaction(3)),
                24 => new Monstermon(id, "Gnomo de Jardín", "Verde", "Mágico",
                                     GetAction(13), GetReaction(1)),
                25 => new Monstermon(id, "Tornado Ofaka", "Verde", "Mágico",
                                     GetAction(4), GetReaction(1)),
                26 => new Monstermon(id, "Literalmente Hierba", "Verde", "Nada Especial",
                                     GetAction(1), GetReaction(3)),
                27 => new Monstermon(id, "Bicho Doodoo", "Verde", "Insecto",
                                     GetAction(15), GetReaction(17)),
                28 => new Monstermon(id, "Tomate Místico", "Verde", "Vegetal",
                                     GetAction(7), GetReaction(17)),
                29 => new Monstermon(id, "Fauna Silbante", "Verde", "Bestia",
                                     GetAction(13), GetReaction(1)),
                30 => new Monstermon(id, "Espada Legendaria", "Verde", "Artefacto",
                                     GetAction(14), GetReaction(0)),
                31 => new Monstermon(id, "Gota de Oro", "Amarillo", "Mágico",
                                     GetAction(19), GetReaction(11)),
                32 => new Monstermon(id, "Pulpo Zen", "Amarillo", "Humanoide",
                                     GetAction(1), GetReaction(11)),
                33 => new Monstermon(id, "Libro Prohibido", "Amarillo", "Artefacto",
                                     GetAction(17), GetReaction(11)),
                34 => new Monstermon(id, "Malvavisco", "Amarillo", "Mágico",
                                     GetAction(16), GetReaction(11)),
                35 => new Monstermon(id, "Olla de Grasa", "Amarillo", "Artefacto",
                                     GetAction(20), GetReaction(11)),
                36 => new Monstermon(id, "Cordero con Cuchilla", "Amarillo", "Bestia",
                                     GetAction(24), GetReaction(11)),
                37 => new Monstermon(id, "Cofre del Tesoro", "Amarillo", "Artefacto",
                                     GetAction(25), GetReaction(11)),
                38 => new Monstermon(id, "Mr. Nice Guy", "Amarillo", "Humanoide",
                                     GetAction(18), GetReaction(11)),
                39 => new Monstermon(id, "The Slurper", "Amarillo", "Repugnante",
                                     GetAction(27), GetReaction(11)),
                40 => new Monstermon(id, "Joya Rara", "Amarillo", "Artefacto",
                                     GetAction(26), GetReaction(11)),
                41 => new Monstermon(id, "Cebolla", "Púrpura", "Vegetal",
                                     GetAction(1), GetReaction(10)),
                42 => new Monstermon(id, "Ojo Asesino", "Púrpura", "Órgano",
                                     GetAction(8), GetReaction(10)),
                43 => new Monstermon(id, "Plush Púrpura", "Púrpura", "Mágico",
                                     GetAction(16), GetReaction(10)),
                44 => new Monstermon(id, "Spiky Flim Flam", "Púrpura", "Bestia",
                                     GetAction(24), GetReaction(10)),
                45 => new Monstermon(id, "Fantasma Monstruoso", "Púrpura", "Bestia",
                                     GetAction(10), GetReaction(10)),
                46 => new Monstermon(id, "Caballero que se Volvió Malvado", "Púrpura", "Humanoide",
                                     GetAction(25), GetReaction(10)),
                47 => new Monstermon(id, "Frustrador del Mal", "Púrpura", "Bestia",
                                     GetAction(26), GetReaction(10)),
                48 => new Monstermon(id, "Paquete Misterioso", "Púrpura", "Artefacto",
                                     GetAction(17), GetReaction(10)),
                49 => new Monstermon(id, "Ogrobop Ogle", "Púrpura", "Bestia",
                                     GetAction(9), GetReaction(10)),
                50 => new Monstermon(id, "Mago Dank", "Púrpura", "Mágico",
                                     GetAction(27), GetReaction(10)),
                _ => new Monstermon(0, "<NULO>", "Azul", "Bestia",
                                    GetAction(1), GetReaction(2))
            };
            __result = monstermon;
            return false;
        }

        private static CardAction GetAction(int id) {
            CardAction action = id switch {
                1 => new CardAction(id, 1, "Inflige 2 de daño.", 2),
                2 => new CardAction(id, 1, "Inflige 2 de daño, +2 si tu carta superior descartada es Roja.", 2,
                                    2, ["Descartar", "Roja"]),
                3 => new CardAction(id, 1, "Inflige 2 de daño, +2 si tu carta superior descartada es Azul.", 2,
                                    2, ["Descartar", "Azul"]),
                4 => new CardAction(id, 1, "Inflige 2 de daño, +2 si tu carta superior descartada es Verde.", 2,
                                    2, ["Descartar", "Verde"]),
                5 => new CardAction(id, 1, "Inflige 2 de daño, +2 si tu carta superior descartada es Amarilla.", 2,
                                    2, ["Descartar", "Amarilla"]),
                6 => new CardAction(id, 1, "Inflige 2 de daño, +2 si tu carta superior descartada es Púrpura.", 2,
                                    2, ["Descartar", "Púrpura"]),
                7 => new CardAction(id, 0, "Inflige 1 de daño. Gana 1 Mana.", 1,
                                    1, ["Mana", "1"]),
                8 => new CardAction(id, 2, "Inflige 3 de daño, +3 si tu mano es toda Roja.", 3,
                                    3, ["Mano", "Roja"]),
                9 => new CardAction(id, 2, "Inflige 3 de daño, +3 si tu mano es toda Azul.", 3,
                                    3, ["Mano", "Azul"]),
                10 => new CardAction(id, 2, "Inflige 3 de daño, +3 si tu mano es toda Verde.", 3,
                                     3, ["Mano", "Verde"]),
                11 => new CardAction(id, 2, "Inflige 3 de daño, +3 si tu mano es toda Amarilla.", 3,
                                     3, ["Mano", "Amarilla"]),
                12 => new CardAction(id, 2, "Inflige 3 de daño, +3 si tu mano es toda Púrpura.", 3,
                                     3, ["Mano", "Púrpura"]),
                13 => new CardAction(id, 2, "Inflige 4 de daño.", 4),
                14 => new CardAction(id, 3, "Inflige 6 de daño.", 6),
                15 => new CardAction(id, 2, "Inflige 3 de daño. +2 si tienes menos HP.", 3,
                                     2, ["MenosHP"]),
                16 => new CardAction(id, 3, "Intercambia HP con tu oponente.", 0,
                                     1, ["IntercambiarHP"]),
                17 => new CardAction(id, 1, "Intercambia Mana con tu oponente.", 0,
                                     1, ["IntercambiarMana"]),
                18 => new CardAction(id, 2, "Cura 4 HP si tu descarte es Rojo", 0,
                                     1, ["Curar", "Rojo"]),
                19 => new CardAction(id, 2, "Cura 4 HP si tu descarte es Azul", 0,
                                     1, ["Curar", "Azul"]),
                20 => new CardAction(id, 2, "Cura 4 HP si tu descarte es Verde", 0,
                                     1, ["Curar", "Verde"]),
                21 => new CardAction(id, 3, "Inflige 1 de daño. Reduce el Mana del oponente en 1.", 1,
                                     1, ["Reducir_Mana", "1"]),
                22 => new CardAction(id, 2, "Copia la acción de tu descarte.", 0,
                                     1, ["Copiar", "Mío"]),
                23 => new CardAction(id, 2, "Copia la acción del descarte del oponente.", 0,
                                     1, ["Copiar", "Oponente"]),
                24 => new CardAction(id, 1, "Gana 3 Mana si tu descarte es Rojo", 0,
                                     1, ["Mana_Descartado", "Rojo"]),
                25 => new CardAction(id, 1, "Gana 3 Mana si tu descarte es Azul", 0,
                                     1, ["Mana_Descartado", "Azul"]),
                26 => new CardAction(id, 1, "Gana 3 Mana si tu descarte es Verde", 0,
                                     1, ["Mana_Descartado", "Verde"]),
                27 => new CardAction(id, 5, "Inflige 8 de daño.", 8),
                _ => new CardAction(id, 0, "", 0)
            };
            return action;
        }

        private static CardReaction GetReaction(int id) {
            CardReaction reaction = id switch {
                1 => new CardReaction(1, "Reduce el daño de un Monstermon en 1.", 1, ["Todos"]),
                2 => new CardReaction(1, "Reduce el daño de un Monstermon Rojo en 3.", 3, ["Rojo"]),
                3 => new CardReaction(1, "Reduce el daño de un Monstermon Azul en 3.", 3, ["Azul"]),
                4 => new CardReaction(1, "Reduce el daño de un Monstermon Verde en 3.", 3, ["Verde"]),
                5 => new CardReaction(1, "Reduce el daño de un Monstermon Amarillo en 3.", 3, ["Amarillo"]),
                6 => new CardReaction(1, "Reduce el daño de un Monstermon Púrpura en 3.", 3, ["Púrpura"]),
                7 => new CardReaction(2, "Bloquea un Monstermon Rojo.", 99, ["Rojo"]),
                8 => new CardReaction(2, "Bloquea un Monstermon Azul.", 99, ["Azul"]),
                9 => new CardReaction(2, "Bloquea un Monstermon Verde.", 99, ["Verde"]),
                10 => new CardReaction(1, "Bloquea un Monstermon Amarillo.", 99, ["Amarillo"]),
                11 => new CardReaction(1, "Bloquea un Monstermon Púrpura.", 99, ["Púrpura"]),
                12 => new CardReaction(2, "Reduce el daño de un Monstermon Rojo o Azul en 3.", 3,
                                       ["Rojo", "Azul"]),
                13 => new CardReaction(2, "Reduce el daño de un Monstermon Rojo o Verde en 3.", 3,
                                       ["Rojo", "Verde"]),
                14 => new CardReaction(2, "Reduce el daño de un Monstermon Azul o Verde en 3.", 3,
                                       ["Azul", "Verde"]),
                15 => new CardReaction(2, "Reduce el daño de un Monstermon Amarillo o Púrpura en 3.", 3,
                                       ["Amarillo", "Púrpura"]),
                16 => new CardReaction(3, "Refleja un Monstermon Rojo.", -1, ["Rojo"]),
                17 => new CardReaction(3, "Refleja un Monstermon Azul.", -1, ["Azul"]),
                18 => new CardReaction(3, "Refleja un Monstermon Verde.", -1, ["Verde"]),
                19 => new CardReaction(3, "Refleja un Monstermon Amarillo.", -1, ["Amarillo"]),
                20 => new CardReaction(3, "Refleja un Monstermon Púrpura.", -1, ["Púrpura"]),
                _ => new CardReaction(0, "", 0, [""])
            };
            return reaction;
        }*/

        // Debug to unlock all
        /*[HarmonyPatch(typeof(SaveLoadFile), "LoadKG2", typeof(int)), HarmonyPrefix]
        public static bool LoadKG2(ref SaveData __result, int x) {
            __result = new SaveData(true, x);
            return false;
        }*/

        private static string GetRunningDirectory() {
            DirectoryInfo parentPath = Directory.GetParent(Application.dataPath);
            return parentPath?.FullName;
        }
    }
}