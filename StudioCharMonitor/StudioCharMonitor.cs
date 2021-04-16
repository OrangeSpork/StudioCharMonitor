using AIChara;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using Manager;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace StudioCharMonitor
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInProcess("StudioNEOV2")]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    public class StudioCharMonitor : BaseUnityPlugin
    {
        public const string VERSION = "1.0.0";
        public const string GUID = "orange.spork.studiocharmon";
        public const string NAME = "Studio Character Monitor";

        private static ConfigEntry<KeyboardShortcut> _showMonitor;
        private static ConfigEntry<CounterColors> _monitorColor;
        private static ConfigEntry<TextAnchor> _position;
        private static ConfigEntry<bool> _shown;

        private static ConfigEntry<bool> _kinematics;
        private static ConfigEntry<bool> _animation;
        private static ConfigEntry<bool> _clothes;
        private static ConfigEntry<bool> _accessories;
        private static ConfigEntry<bool> _expression;
        private static ConfigEntry<bool> _state;
        private static ConfigEntry<bool> _look;

        internal ManualLogSource Log => Logger;

        public static StudioCharMonitor Instance;


        const int MAX_STRING_SIZE = 1999;

        private static readonly GUIStyle _style = new GUIStyle();
        private static Rect _screenRect;
        private const int ScreenOffset = 10;

        private static MutableString fString = new MutableString(MAX_STRING_SIZE, true);
        private static string _frameOutputText;

        public StudioCharMonitor()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Singleton only.");
            }

            Instance = this;
        }

        private void Start()
        {
            DetectMoreAccessories();

            _showMonitor = Config.Bind("General", "Toggle monitor", new KeyboardShortcut(KeyCode.M, KeyCode.LeftShift), "Key to enable and disable the plugin.");
            _shown = Config.Bind("General", "Enable", false, "Show Currently Selected Character Status.");

            _position = Config.Bind("Interface", "Screen position", TextAnchor.LowerRight, "Which corner of the screen to display the status in.");
            _monitorColor = Config.Bind("Interface", "Color of the text", CounterColors.White, "Color of the displayed status.");


            _kinematics = Config.Bind("Options", "Show Kinematics Information", true, "Show IK/FK Information");            
            _animation = Config.Bind("Options", "Show Animation Information", true, "Show Animation Information");        
            _clothes = Config.Bind("Options", "Show Clothes State Information", true, "Clothing States");         
            _accessories = Config.Bind("Options", "Show Acc State", false, "Accessory Visibility State");        
            _expression = Config.Bind("Options", "Show Face/Hand State", true, "Eyebrows, Eyes, Mouth, Hands Information");        
            _state = Config.Bind("Options", "Show Misc State", true, "Blush, Gloss, Wetness, etc");        
            _look = Config.Bind("Options", "Show Eye/Neck Look State", true, "Show Eye and Neck Controller States");

        _position.SettingChanged += (sender, args) => UpdateLooks();
            _monitorColor.SettingChanged += (sender, args) => UpdateLooks();
            _shown.SettingChanged += (sender, args) =>
            {
                UpdateLooks();
            };

            OnEnable();
        }

        public static object MoreAccessoriesInstance { get; set; }
        public static Type MoreAccessoriesType { get; set; }
        private static FieldInfo additionalDataField;
        private static FieldInfo objectsField;
        private static FieldInfo showField;

        // Soft Link Reference to More Accessories
        private static void DetectMoreAccessories()
        {
            try
            {
                MoreAccessoriesType = Type.GetType("MoreAccessoriesAI.MoreAccessories, MoreAccessories", false);
                if (MoreAccessoriesType != null)
                {
                    additionalDataField = AccessTools.Field(MoreAccessoriesType, "_charAdditionalData");
                    objectsField = AccessTools.Field(MoreAccessoriesType.GetNestedType("AdditionalData", AccessTools.all), "objects");
                    showField = AccessTools.Field(MoreAccessoriesType.GetNestedType("AdditionalData", AccessTools.all).GetNestedType("AccessoryObject", AccessTools.all), "show");

                    MoreAccessoriesInstance = BepInEx.Bootstrap.Chainloader.ManagerObject.GetComponent(MoreAccessoriesType);
                }
            }
            catch (Exception e)
            {
                MoreAccessoriesType = null;
                Instance.Logger.LogWarning($"More Accessories appears to be missing...{e}");
            }
        }

        private void Update()
        {
            if (_showMonitor.Value.IsDown())
                _shown.Value = !_shown.Value;
        }

        private void LateUpdate()
        {
            IEnumerable<OCIChar> characters = KKAPI.Studio.StudioAPI.GetSelectedCharacters();
            OCIChar selectedCharacter = characters.FirstOrDefault();
            if (selectedCharacter != null)
            {
                fString.Append($"Name: {selectedCharacter.charInfo.fileParam.fullname} Show: {selectedCharacter.charInfo.enabled} Pos: {selectedCharacter.charInfo.gameObject.transform.position} Rot: {selectedCharacter.charInfo.gameObject.transform.rotation.eulerAngles}\n");
                // Kinematics
                if (_kinematics.Value)
                {
                    if (selectedCharacter.oiCharInfo.enableIK)
                    {
                        fString.Append("IK Active:");
                        if (selectedCharacter.oiCharInfo.activeIK[0])
                            fString.Append(" Body ");
                        if (selectedCharacter.oiCharInfo.activeIK[4])
                            fString.Append(" LHand ");
                        if (selectedCharacter.oiCharInfo.activeIK[3])
                            fString.Append(" RHand ");
                        if (selectedCharacter.oiCharInfo.activeIK[2])
                            fString.Append(" LLeg ");
                        if (selectedCharacter.oiCharInfo.activeIK[1])
                            fString.Append(" RLeg ");
                        if (!selectedCharacter.oiCharInfo.activeIK.Any(ik => ik))
                            fString.Append(" None ");
                        fString.Append(" | ");
                    }
                    else
                    {
                        fString.Append("IK Disabled | ");
                    }
                    if (selectedCharacter.oiCharInfo.enableFK)
                    {
                        fString.Append("FK Active:");
                        if (selectedCharacter.oiCharInfo.activeFK[0])
                            fString.Append(" Hair ");
                        if (selectedCharacter.oiCharInfo.activeFK[1])
                            fString.Append(" Neck ");
                        if (selectedCharacter.oiCharInfo.activeFK[2])
                            fString.Append(" Chest ");
                        if (selectedCharacter.oiCharInfo.activeFK[3])
                            fString.Append(" Body ");
                        if (selectedCharacter.oiCharInfo.activeFK[5])
                            fString.Append(" LHand ");
                        if (selectedCharacter.oiCharInfo.activeFK[4])
                            fString.Append(" RHand ");
                        if (selectedCharacter.oiCharInfo.activeFK[6])
                            fString.Append(" Skirt ");
                        if (!selectedCharacter.oiCharInfo.activeFK.Any(fk => fk))
                            fString.Append(" None ");
                        fString.Append("\n");
                    }
                    else
                    {
                        fString.Append("FK Disabled\n");
                    }
                }

                // Animation
                if (_animation.Value)
                {
                    if (selectedCharacter.oiCharInfo.animeInfo != null)
                    {
                        string groupName = Studio.Info.Instance.dicAGroupCategory[selectedCharacter.oiCharInfo.animeInfo.group]?.name;
                        string categoryName = Studio.Info.Instance.dicAGroupCategory[selectedCharacter.oiCharInfo.animeInfo.group]?.dicCategory[selectedCharacter.oiCharInfo.animeInfo.category]?.name;
                        string animationName = Studio.Info.Instance.dicAnimeLoadInfo[selectedCharacter.oiCharInfo.animeInfo.group]?[selectedCharacter.oiCharInfo.animeInfo.category]?[selectedCharacter.oiCharInfo.animeInfo.no]?.name;
                        fString.Append($"Anim: {groupName}-{categoryName}-{animationName} Time: { ((selectedCharacter.charAnimeCtrl.animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1.0f) * selectedCharacter.charAnimeCtrl.animator.GetCurrentAnimatorStateInfo(0).length):000.0}:{selectedCharacter.charAnimeCtrl.animator.GetCurrentAnimatorStateInfo(0).length:000.0} Spd: {selectedCharacter.animeSpeed:0.###} Pat: {selectedCharacter.animePattern:0.###} E1: {selectedCharacter.animeOptionParam1:0.###} E2: {selectedCharacter.animeOptionParam2:0.###} Items: {selectedCharacter.oiCharInfo.animeOptionVisible} Loop: {selectedCharacter.oiCharInfo.isAnimeForceLoop}\n");
                    }
                }

                if (_clothes.Value)
                {
                    fString.Append($"Clothes: Top: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.top])} Bot: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.bot])} InnerT: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.inner_t])} InnerB: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.inner_b])} Panty: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.panst])} Gloves: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.gloves])} Socks: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.socks])} Shoes: {TranslateClothesState(selectedCharacter.charFileStatus.clothesState[(int)ChaFileDefine.ClothesKind.shoes])}\n");
                }

                if (_accessories.Value)
                {
                    int i = 0;
                    Nullable<bool> showing = IsAccessoryShowing(i, selectedCharacter.charInfo);
                    fString.Append("Acc: ");
                    while (showing.HasValue)
                    {
                        fString.Append($"{i + 1}:{(showing.Value ? "O" : "X")} ");
                        showing = IsAccessoryShowing(++i, selectedCharacter.charInfo);
                    }
                    fString.Append("\n");
                }

                if (_expression.Value)
                {
                    fString.Append($"Brows: {TranslateEyebrowId(selectedCharacter.charInfo.fileStatus.eyebrowPtn, selectedCharacter)} Eyes: {TranslateEyeId(selectedCharacter.charInfo.fileStatus.eyesPtn, selectedCharacter)} Open: {selectedCharacter.charInfo.eyesCtrl.OpenMax:0.###} Blink: {selectedCharacter.charInfo.fileStatus.eyesBlink} Mouth: {selectedCharacter.charInfo.fileStatus.mouthPtn} Open: {selectedCharacter.oiCharInfo.mouthOpen:0.###} L Hand {selectedCharacter.oiCharInfo.handPtn[0]} R Hand {selectedCharacter.oiCharInfo.handPtn[1]}\n");
                }
                if (_state.Value)
                {
                    fString.Append($"Tears: {selectedCharacter.charInfo.fileStatus.tearsRate:0.###} Flush: {selectedCharacter.charInfo.fileStatus.hohoAkaRate:0.###} Nipple: {selectedCharacter.charInfo.fileStatus.nipStandRate:0.###} Gloss: {selectedCharacter.charInfo.fileStatus.skinTuyaRate:0.###} Wet: {selectedCharacter.charInfo.fileStatus.wetRate:0.###} ");
                    if(!_look.Value)
                    {
                        fString.Append("\n");
                    }
                }
                if (_look.Value)
                {
                    fString.Append($"Gaze: {TranslateEyeLookPtn(selectedCharacter.charInfo.fileStatus.eyesLookPtn)} Neck: {TranslateNeckLookPtn(selectedCharacter.charInfo.fileStatus.neckLookPtn)}\n");
                }
            }
            else
            {
                fString.Append("No Character Selected\n");
            }
            _frameOutputText = fString.Finalize();
        }

        private Nullable<bool> IsAccessoryShowing(int slotNumber, ChaControl chaControl)
        {
            if (slotNumber < 20)
            {
                return chaControl.fileStatus.showAccessory[slotNumber];
            }
            else
            {
                return GetMoreAccessorySlotStatus(slotNumber - 20, chaControl);
            }
        }
        
        private Nullable<bool> GetMoreAccessorySlotStatus(int slot, ChaControl chaControl)
        {
            IDictionary charAdditionalData = (IDictionary)additionalDataField.GetValue(MoreAccessoriesInstance);
            foreach (DictionaryEntry entry in charAdditionalData)
            {
                if (entry.Key.Equals(chaControl.chaFile))
                {
                    IList objectList = (IList)objectsField.GetValue(entry.Value);
                    if (slot >= objectList.Count)
                    {
                        return null;
                    }
                    else
                    {
                        return (bool)showField.GetValue(objectList[slot]);
                    }
                }
            }
            return null;
        }

        private int TranslateEyebrowId(int id, OCIChar character)
        {
            int[] eyebrowsKeys = Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo((character.sex == 0) ? ChaListDefine.CategoryNo.custom_eyebrow_m : ChaListDefine.CategoryNo.custom_eyebrow_f).Keys.ToArray();            
            return Mathf.Clamp(Array.FindIndex(eyebrowsKeys, (int _i) => _i == character.charInfo.GetEyebrowPtn()), 0, eyebrowsKeys.Length - 1);
        }

        private int TranslateEyeId(int id, OCIChar character)
        {
            int[] eyesKeys = Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo((character.sex == 0) ? ChaListDefine.CategoryNo.custom_eye_m : ChaListDefine.CategoryNo.custom_eye_f).Keys.ToArray();
            return Mathf.Clamp(Array.FindIndex(eyesKeys, (int _i) => _i == character.charInfo.GetEyesPtn()), 0, eyesKeys.Length - 1);
        }

        private string TranslateNeckLookPtn(int ptn)
        {
            switch (ptn)
            {
                case 0:
                    return "Front";
                case 1:
                    return "Follow";
                case 3:
                    return "Anim";
                case 4:
                    return "Fixed";
                default:
                    return ptn.ToString();
            }
        }
        private string TranslateEyeLookPtn(int ptn)
        {
            switch (ptn)
            {
                case 0:
                    return "Front";
                case 1:
                    return "Follow";
                case 2:
                    return "Avert";
                case 3:
                    return "Fixed";
                case 4:
                    return "Adjust";
                default:
                    return ptn.ToString();
            }
        }

        private string TranslateClothesState(int state)
        {
            switch (state)
            {
                case 0:
                    return "ON";
                case 1:
                    return "HALF";
                case 2:
                    return "OFF";
                default:
                    return "OFF";
            }
        }

        private static void UpdateLooks()
        {
            if (_monitorColor.Value == CounterColors.White)
                _style.normal.textColor = Color.white;
            if (_monitorColor.Value == CounterColors.Black)
                _style.normal.textColor = Color.black;

            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            _style.alignment = _position.Value;
            _style.fontSize = h / 65;
        }

        private void OnEnable()
        {
            if (_shown != null && _shown.Value)
            {
                UpdateLooks();
            }
        }

        private void OnDisable()
        {
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint)
                DrawMonitor();
        }

        private static void DrawMonitor()
        {
            if (_monitorColor.Value == CounterColors.Outline)
                ShadowAndOutline.DrawOutline(_screenRect, _frameOutputText, _style, Color.black, Color.white, 1.5f);
            else
                GUI.Label(_screenRect, _frameOutputText, _style);
        }

    }



    public enum CounterColors
    {
        White,
        Black,
        Outline
    }
}
