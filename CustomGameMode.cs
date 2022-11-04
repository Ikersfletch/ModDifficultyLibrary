using System;
using MonoMod;
using Mono.Cecil;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using System.IO;
using Terraria.IO;
using Terraria.ModLoader.Exceptions;
using Terraria.Utilities;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Reflection; // this is just to change some text on the world select screen, lol
using ReLogic.Content;
using Terraria;
using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;
using OnWorldItem = On.Terraria.GameContent.UI.Elements.UIWorldListItem;
using OnUI = On.Terraria.UI.UserInterface;
using OnUIWorldCreation = On.Terraria.GameContent.UI.States.UIWorldCreation;
using OnWorldGen = On.Terraria.WorldGen;
using OnUIWorldCreationPreview = On.Terraria.GameContent.UI.Elements.UIWorldCreationPreview;
using ILUIWorldCreation = IL.Terraria.GameContent.UI.States.UIWorldCreation;
using Terraria.GameContent.UI.States;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI.Gamepad;
using MonoMod.Cil;
using System.Linq;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using System.Reflection.Emit;
using OpCode = System.Reflection.Emit.OpCode;
using MonoMod.Utils;
using System.ComponentModel;
using Terraria.WorldBuilding;
using log4net.Core;

namespace ModDifficultyLibrary
{
    /// <summary>
    /// Represents a Custom GameMode.
    /// </summary>
    public abstract class ModDifficulty : IModType
    {
        /// <summary>
        /// The colored name displayed on the world select screen and world creation screen
        /// </summary>
        public virtual string DisplayName => "Mod Difficulty";
        /// <summary>
        /// The description which fills the box on the world creation screen
        /// </summary>
        public virtual string Description => "only for REAL GAMERS!!! DIE INSTANTLY!!! all enemies are IMPOSSIBLE!! REAL GAMERS SUFFER WITH THIS DIFFICULTY";
        /// <summary>
        /// The text color for the DisplayName on the World Creation & World Selection screens
        /// </summary>
		public virtual Color DifficultyColor => Color.White;
        /// <summary>
        /// The bunny icon for the difficulty selection button on the world creation menu
        /// </summary>
        public virtual Asset<Texture2D> WorldCreationButtonIcon => Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/IconDifficultyNormal");
        /// <summary>
        /// The bunny in the preview image in the top-right of the world creation menu
        /// </summary>
        public virtual Asset<Texture2D> WorldCreationPreviewBunny => Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyNormal2");
        /// <summary>
        /// The sky in the preview image in the top-right of the world creation menu
        /// </summary>
        public virtual Asset<Texture2D> WorldCreationPreviewSky => Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyNormal1");
        /// <summary>
        /// A color tint over the preview image in the top-right of the world creation menu
        /// </summary>
        public virtual Color WorldCreationPreviewTint => Color.White;
        public virtual string FullName => $"{Mod.Name}/{Name}";
        public virtual string Name => GetType().Name;
        public virtual Mod Mod => ModDifficultyLibrary.Instance;

        /// <summary>
        /// The vanilla data values for enemy & NPC scaling. Includes toggles for Expert mode and Master mode exclusive behavior
        /// </summary>
        public virtual GameModeData GameModeData => GameModeData.NormalMode;

        /// <summary>
        /// Returns true when the Mode of the specified type is the current mode
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool IsModeActive<T>() where T : ModDifficulty
        {
            ModDifficulty dif = GetModDifficultyDirect<T>();
            if (dif == null) return false;
            return dif.IsModeActive();
        }
        /// <summary>
        /// Returns true when the Difficulty matching these parameters is loaded
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool IsModeActive(string origin, string difficultyName)
        {
            Tuple<int, string, string> dif = GameModeSystem.Instance.CurrentDifficulty;
            return dif.Item1 == 4 && dif.Item2 == origin && dif.Item3 == difficultyName;
        }

        /// <summary>
        /// Returns true while playing in a world with this difficulty selected
        /// </summary>
        /// <returns></returns>
        public bool IsModeActive()
        {
            Tuple<int, string, string> dif = GameModeSystem.Instance.CurrentDifficulty;
            return dif.Item1 == 4 && dif.Item2 == Mod.Name && dif.Item3 == Name;
        }
        /// <summary>
        /// Returns the ID of the difficulty. Don't use this value in save data, as the loading of mods can disrupt which difficulty is given which ID.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetModDifficulty<T>() where T : ModDifficulty
        {
            for (int i = 0; i < GameModeSystem.Instance.moddedDifficulties.Count; i++)
            {
                if (GameModeSystem.Instance.moddedDifficulties[i] as T != null) return i;
            }
            return -1;
        }
        /// <summary>
        /// Returns the Instance of the difficulty
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ModDifficulty GetModDifficultyDirect<T>() where T : ModDifficulty
        {
            int index = GetModDifficulty<T>();
            if (index == -1) return null;
            return GameModeSystem.Instance.moddedDifficulties[index];
        }

        public static Tuple<int, string, string> GetCurrentMode()
        {
            return GameModeSystem.Instance.CurrentDifficulty;
        }

        public void Load(Mod mod)
        {
            GameModeSystem.Instance.moddedDifficulties.Add(this);
        }

        public void Unload()
        {
            GameModeSystem.Instance.moddedDifficulties.Remove(this);
        }
    }


    public class GameModeSystem : ModSystem
    {
        public static GameModeSystem Instance => ModContent.GetInstance<GameModeSystem>();
        public List<ModDifficulty> moddedDifficulties = new List<ModDifficulty>();

        public int moddedDifficultyIndex = -1;

        public static GameModeData registeredMode = GameModeData.NormalMode;

        internal struct FileEntry<T> where T : FileData
        {
            public static Dictionary<FileEntry<T>, byte[]> cache = new Dictionary<FileEntry<T>, byte[]>();

            public readonly string path;
            public readonly bool cloud;

            public FileEntry(string path, bool cloud)
            {
                this.path = path;
                this.cloud = cloud;
            }

            public override int GetHashCode()
                => HashCode.Combine(path, cloud);
        }
        public static TagCompound LoadWorldTagData<T>(string path, bool isCloudSave) where T : ModSystem
        {
            //A compressed version of WorldIO.Load
            path = Path.ChangeExtension(path, ".twld");

            if (!FileUtilities.Exists(path, isCloudSave))
                return null;

            FileEntry<WorldFileData> entry = new(path, isCloudSave);

            if (!FileEntry<WorldFileData>.cache.TryGetValue(entry, out byte[] buf) || buf is null)
                FileEntry<WorldFileData>.cache[entry] = buf = FileUtilities.ReadAllBytes(path, isCloudSave);

            if (buf[0] != 0x1F || buf[1] != 0x8B)
            {
                //LoadLegacy(buf);
                return null;
            }

            var tag = TagIO.FromStream(new MemoryStream(buf));

            foreach (var data in tag.GetList<TagCompound>("modData"))
            {
                if (ModContent.TryFind(data.GetString("mod"), data.GetString("name"), out ModSystem system) && system is T typedSystem)
                {
                    try
                    {
                        return data.GetCompound("data");
                    }
                    catch (Exception e)
                    {
                        throw new CustomModDataException(system.Mod,
                            "Error in reading custom world data for " + system.Mod.Name, e);
                    }
                }
            }

            return null;
        }



        // I would make this Load(), but I'm choosing to be safe instead.
        public override void OnModLoad()
        {
            Main.RegisteredGameModes.Add(4, registeredMode);
            OnWorldItem.DrawSelf += DrawWorldSelectItem;
            //OnUI.SetState += ModifySetState;
            OnUIWorldCreation.BuildPage += BuildPage;
            OnUIWorldCreation.AddWorldDifficultyOptions += AddWorldDifficultyOptions;
            OnUIWorldCreation.SetDefaultOptions += SetDefaultOptions;
            OnUIWorldCreation.ClickDifficultyOption += ClickDifficultyOption;
            OnUIWorldCreationPreview.DrawSelf += DrawPreviewPlate;
            // OnUIWorldCreation.ShowOptionDescription += ShowOptionDescription;
            /*
            OnUIWorldCreation.UpdatePreviewPlate += UpdatePreviewPlate;
            OnUIWorldCreation.UpdateSliders += UpdateSliders;
            */
            OnWorldGen.CreateNewWorld += CreateNewWorld;
            OnUIWorldCreation.Click_GoBack += Click_GoBack;
        }
        public override void Unload()
        {
            Main.RegisteredGameModes.Remove(4);
            OnWorldItem.DrawSelf -= DrawWorldSelectItem;
            // OnUI.SetState -= ModifySetState;
            OnUIWorldCreation.BuildPage -= BuildPage;
            OnUIWorldCreation.AddWorldDifficultyOptions -= AddWorldDifficultyOptions;
            OnUIWorldCreation.SetDefaultOptions -= SetDefaultOptions;
            OnUIWorldCreation.ClickDifficultyOption -= ClickDifficultyOption;
            OnUIWorldCreationPreview.DrawSelf -= DrawPreviewPlate;
            //OnUIWorldCreation.ShowOptionDescription -= ShowOptionDescription;
            /*
            OnUIWorldCreation.UpdatePreviewPlate -= UpdatePreviewPlate;
            OnUIWorldCreation.UpdateSliders -= UpdateSliders;
            */
            OnWorldGen.CreateNewWorld -= CreateNewWorld;
            OnUIWorldCreation.Click_GoBack -= Click_GoBack;
        }

        public override void OnWorldLoad()
        {
            if (Main.ActiveWorldFileData.GameMode != 4 || moddedDifficultyIndex == -1) return;
            Main.RegisteredGameModes.Remove(4);
            Main.RegisteredGameModes.Add(4,moddedDifficulties[moddedDifficultyIndex].GameModeData);
            Main.GameMode = 4;

        }
        public override void OnWorldUnload()
        {
            if (Main.GameMode != 4) return;
        }



        public ModDifficulty GetGameMode(string modOriginName, string modDifficultyName)
        {
            Predicate<ModDifficulty> works = mode => mode.Mod.Name == modOriginName && mode.Name == modDifficultyName;
            for (int i = 0; i < moddedDifficulties.Count; i++)
                if (works(moddedDifficulties[i])) return moddedDifficulties[i];
            return null;
        }

        public void ModifySetState(OnUI.orig_SetState orig, UserInterface self, UIState state)
        {
            if (state is UIWorldCreation)
            {
                orig(self, new CustomWorldCreationUI());
                return;
            }
            orig(self, state);
        }


        public void GetGameModeIdentifiers(ModDifficulty mode, out string modOriginName, out string modDifficultyName)
        {
            if (mode == null)
            {
                modOriginName = null;
                modDifficultyName = null;
                return;
            }

            modOriginName = mode.Mod.Name;
            modDifficultyName = mode.Name;
        }


        public Tuple<int, string, string> CurrentDifficulty = new Tuple<int, string, string>(0, "Vanilla", "Classic");

        public override void SaveWorldData(TagCompound tag)
        {
            if (Main.GameMode == 4)
            {
                tag.Add("ModDifficultySource", CurrentDifficulty.Item2);
                tag.Add("ModDifficultyName", CurrentDifficulty.Item3);
            }
        }

        public static Tuple<int, string, string> JourneyTuple => new Tuple<int, string, string>(3, "Terraria", "Journey");
        public static Tuple<int, string, string> ClassicTuple => new Tuple<int, string, string>(0, "Terraria", "Classic");
        public static Tuple<int, string, string> ExpertTuple => new Tuple<int, string, string>(1, "Terraria", "Expert");
        public static Tuple<int, string, string> MasterTuple => new Tuple<int, string, string>(2, "Terraria", "Master");

        public override void LoadWorldData(TagCompound tag)
        {
            switch (Main.ActiveWorldFileData.GameMode)
            {
                case 0:
                    CurrentDifficulty = ClassicTuple;
                    return;
                case 1:
                    CurrentDifficulty = ExpertTuple;
                    return;
                case 2:
                    CurrentDifficulty = MasterTuple;
                    return;
                case 3:
                    CurrentDifficulty = JourneyTuple;
                    return;
            }

            if (!tag.TryGet("ModDifficultySource", out string origin)) return;
            if (!tag.TryGet("ModDifficultyName", out string difficultyName)) return;
            CurrentDifficulty = new Tuple<int, string, string>(4, origin, difficultyName);

            ModDifficulty mode = GetGameMode(origin, difficultyName);

            Main.RegisteredGameModes.Remove(4);
            if (mode == null) Main.RegisteredGameModes[4] = GameModeData.NormalMode;
            else Main.RegisteredGameModes[4] = mode.GameModeData;
            Main.GameMode = 4;
        }

        public Dictionary<WorldFileData, Tuple<string, Color>> cached = new Dictionary<WorldFileData, Tuple<string, Color>>();

        public void DrawWorldSelectItem(OnWorldItem.orig_DrawSelf orig, UIWorldListItem self, SpriteBatch spriteBatch)
        {
            // nooooo!!! you're supposed to use the API so internal changes don't break your code!!!!

            // accessing private fields go brrrrr
            BindingFlags yes = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo dataInfo = self.GetType().GetField("_data", yes);
            WorldFileData data = (WorldFileData)dataInfo.GetValue(self);

            if (data.GameMode != 4) { orig(self, spriteBatch); return; }

            // the default is gray text saying unloaded. that's why we have to go through this whole ordeal if this is a custom difficulty at all.
            string text = "Unloaded";
            Color color2 = Color.Gray;

            if (!cached.TryGetValue(data, out Tuple<string, Color> cachedDraw) || cachedDraw is null)
            {
                TagCompound tags = LoadWorldTagData<GameModeSystem>(data.Path, data.IsCloudSave);

                if (tags == null) goto UnloadedMode;

                if (!tags.TryGet("ModDifficultySource", out string origin)) goto UnloadedMode;
                if (!tags.TryGet("ModDifficultyName", out string difficultyName)) goto UnloadedMode;

                ModDifficulty gameMode = GetGameMode(origin, difficultyName);

                if (gameMode == null) goto UnloadedMode;

                text = gameMode.DisplayName;
                color2 = gameMode.DifficultyColor;

                cached.Add(data, new Tuple<string, Color>(text, color2));
                goto JustDrawIt;
            }
            else
            {
                text = cachedDraw.Item1;
                color2 = cachedDraw.Item2;
                goto JustDrawIt;
            }

        UnloadedMode:
            cached.Add(data, new Tuple<string, Color>(text, color2));

        JustDrawIt:

            // get private fields and methods.
            // I am frustrated that nothing is public, so shit like this has to be done to call these methods.
            var drawPanel = self.GetType().GetMethod("DrawPanel", yes).MethodHandle.GetFunctionPointer();
            var drawPanelAction = (Action<SpriteBatch, Vector2, float>)Activator.CreateInstance(typeof(Action<SpriteBatch, Vector2, float>), self, drawPanel);
            FieldInfo worldIcon = self.GetType().GetField("_worldIcon", yes);
            FieldInfo dividerTex = self.GetType().GetField("_dividerTexture", yes);
            var drawPanelBase = self.GetType().BaseType.GetMethod("DrawSelf", yes).MethodHandle.GetFunctionPointer();
            var baseDrawSelf = (Action<SpriteBatch>)Activator.CreateInstance(typeof(Action<SpriteBatch>), self, drawPanelBase);


            // this is adapted from the orig method to draw the above values.
            // No, I don't care to make this part readable. It's not my code.

            baseDrawSelf(spriteBatch);
            CalculatedStyle innerDimensions = self.GetInnerDimensions();
            CalculatedStyle dimensions = ((UIImage)worldIcon.GetValue(self)).GetDimensions();
            float num = dimensions.X + dimensions.Width;
            Color color = data.IsValid ? Color.White : Color.Gray;
            Utils.DrawBorderString(spriteBatch, data.Name, new Vector2(num + 6f, dimensions.Y - 2f), color);
            spriteBatch.Draw(((Asset<Texture2D>)dividerTex.GetValue(self)).Value, new Vector2(num, innerDimensions.Y + 21f), null, Color.White, 0f, Vector2.Zero, new Vector2((self.GetDimensions().X + self.GetDimensions().Width - num) / 8f, 1f), SpriteEffects.None, 0f);
            Vector2 vector = new Vector2(num + 6f, innerDimensions.Y + 29f);
            float num2 = 100f;
            drawPanelAction(spriteBatch, vector, num2);
            float x = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(text).X;
            float x2 = num2 * 0.5f - x * 0.5f;
            Utils.DrawBorderString(spriteBatch, text, vector + new Vector2(x2, 3f), color2);
            vector.X += num2 + 5f;
            float num3 = 150f;
            if (!GameCulture.FromCultureName(GameCulture.CultureName.English).IsActive) num3 += 40f;
            drawPanelAction(spriteBatch, vector, num3);
            string textValue = Language.GetTextValue("UI.WorldSizeFormat", data.WorldSizeName);
            float x3 = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(textValue).X;
            float x4 = num3 * 0.5f - x3 * 0.5f;
            Utils.DrawBorderString(spriteBatch, textValue, vector + new Vector2(x4, 3f), Color.White);
            vector.X += num3 + 5f;
            float num4 = innerDimensions.X + innerDimensions.Width - vector.X;
            drawPanelAction(spriteBatch, vector, num4);
            string arg = !GameCulture.FromCultureName(GameCulture.CultureName.English).IsActive ? data.CreationTime.ToShortDateString() : data.CreationTime.ToString("d MMMM yyyy");
            string textValue2 = Language.GetTextValue("UI.WorldCreatedFormat", arg);
            float x5 = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(textValue2).X;
            float x6 = num4 * 0.5f - x5 * 0.5f;
            Utils.DrawBorderString(spriteBatch, textValue2, vector + new Vector2(x6, 3f), Color.White);
            vector.X += num4 + 5f;

        }

        // detours galore let's go

        // UIWorldCreation
        public List<CustomGroupButton<Tuple<int, string, string>>> difficultyButtons;
        public Tuple<int, string, string> worldCreationDifficultyValue = ClassicTuple;
        public UIWorldCreation creation_screen;
        public string body;
        public void BuildPage(OnUIWorldCreation.orig_BuildPage orig, UIWorldCreation self) {
            // this reroute just makes the world creation screen larger.
            /*
            MethodInfo info = self.GetType().GetMethod("SetDefaultOptions", yes);
            byte[] arr = info.GetMethodBody().GetILAsByteArray();
            string printme = "";

            for (int i = 0; i < arr.Length; i++)
            {
                printme += arr[i].ToString("X2")+"\n";
            }
            Mod.Logger.Info(printme);
            */

            /*
            */
            orig(self);
            creation_screen = self;
            UIElement baseElement = self.Children.First<UIElement>();
            baseElement.Width = StyleDimension.FromPercent(0.9f);
            baseElement.Height = StyleDimension.FromPixels(434f + 18);
            baseElement.Top = StyleDimension.FromPixels(170f - 18);
            baseElement.HAlign = 0.5f;
            baseElement.VAlign = 0f;



        }

        public void SetDefaultDifficulty()
        {
            if (Main.ActivePlayerFileData.Player.difficulty == 3)
            {
                difficultyButtons.ForEach(i => i.SetCurrentOption(JourneyTuple));
                worldCreationDifficultyValue = JourneyTuple;
            }
            else
            {
                difficultyButtons.ForEach(i => i.SetCurrentOption(ClassicTuple));
                worldCreationDifficultyValue = ClassicTuple;
            }
        }
        public void SetDefaultOptions(OnUIWorldCreation.orig_SetDefaultOptions orig, UIWorldCreation self) {
            /*
            BindingFlags yes = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Type uiworldcreation = creation_screen.GetType();
            PropertyInfo difbuttonInfo = uiworldcreation.GetProperty("_difficultyButtons", yes);
            Type difButtonsVanilla = difbuttonInfo.PropertyType;
            self.GetType().GetProperty("_difficultyButtons").SetValue(creation_screen, Activator.CreateInstance(difButtonsVanilla));
            */
            SetDefaultDifficulty();
            orig(self);

        }
        public UIElement fakeContainerLOL;
        public void AddWorldDifficultyOptions(OnUIWorldCreation.orig_AddWorldDifficultyOptions orig, UIWorldCreation self, UIElement container, float accumualtedHeight, UIElement.MouseEvent clickEvent, string tagGroup, float usableWidthPercent) {
            /*
            */
            if (fakeContainerLOL == null)
            {
                fakeContainerLOL = new UIElement();
            }
            fakeContainerLOL.RemoveAllChildren();
            orig(self, fakeContainerLOL, 0, (arg1,arg2) => { }, tagGroup, 1.0f);


            if (difficultyButtons == null)
            {
                difficultyButtons = new List<CustomGroupButton<Tuple<int, string, string>>>();
            }
            difficultyButtons.Clear();
            List<Tuple<int, string, string>> DifficultyIndex = new List<Tuple<int, string, string>>
            {
                JourneyTuple,
                ClassicTuple,
                ExpertTuple,
                MasterTuple
            };
            List<string> ButtonText = new List<string>
            {
                Language.GetTextValue("UI.Creative"),
                Language.GetTextValue("UI.Normal"),
                Language.GetTextValue("UI.Expert"),
                Language.GetTextValue("UI.Master")
            };
            List<string> DescriptionText = new List<string>
            {
                Language.GetTextValue("UI.WorldDescriptionCreative"),
                Language.GetTextValue("UI.WorldDescriptionNormal"),
                Language.GetTextValue("UI.WorldDescriptionExpert"),
                Language.GetTextValue("UI.WorldDescriptionMaster")
            };
            List<Color> TextColor = new List<Color>
            {
                Main.creativeModeColor,
                Color.White,
                Main.mcColor,
                Main.hcColor
            };
            List<Asset<Texture2D>> Icons = new List<Asset<Texture2D>>
            {
                Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/IconDifficultyCreative"),
                Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/IconDifficultyNormal"),
                Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/IconDifficultyExpert"),
                Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/IconDifficultyMaster")
            };

            GameModeSystem.Instance.moddedDifficulties.ForEach(difficulty =>
            {
                DifficultyIndex.Add(new Tuple<int, string, string>(4, difficulty.Mod.Name, difficulty.Name));
                ButtonText.Add(difficulty.DisplayName);
                DescriptionText.Add(difficulty.Description);
                TextColor.Add(difficulty.DifficultyColor);
                Icons.Add(difficulty.WorldCreationButtonIcon);
            });

            int buttonCount = DifficultyIndex.Count;

            for (int i = 0; i < buttonCount; i++)
            {
                CustomGroupButton<Tuple<int, string, string>> button = new CustomGroupButton<Tuple<int, string, string>>(DifficultyIndex[i], ButtonText[i], DescriptionText[i], TextColor[i], Icons[i], 1f, 1f, 16f);
                button.Width = StyleDimension.FromPixelsAndPercent(-1 * (buttonCount - 1), 1f / buttonCount * usableWidthPercent);
                button.Left = StyleDimension.FromPercent(1f - usableWidthPercent);
                button.HAlign = i / (float)(buttonCount - 1);
                button.Top.Set(accumualtedHeight, 0f);
                button.OnMouseDown += clickEvent;
                button.OnMouseOver += ShowOptionDescription;
                button.OnMouseOut += self.ClearOptionDescription;
                button.SetSnapPoint(tagGroup, i);
                container.Append(button);
                difficultyButtons.Add(button);
            }
            //orig(self, container, accumualtedHeight, clickEvent, tagGroup, usableWidthPercent);
        }
        public void ClickDifficultyOption(OnUIWorldCreation.orig_ClickDifficultyOption orig, UIWorldCreation self, UIMouseEvent evt, UIElement listeningElement)
        {
            CustomGroupButton<Tuple<int, string, string>> groupOptionButton = (CustomGroupButton<Tuple<int, string, string>>)listeningElement;
            worldCreationDifficultyValue = groupOptionButton.OptionValue;
            difficultyButtons.ForEach(button => button.SetCurrentOption(worldCreationDifficultyValue));
        }
        public void UpdatePreviewPlate(OnUIWorldCreation.orig_UpdatePreviewPlate orig, UIWorldCreation self) { }
        public void UpdateSliders(OnUIWorldCreation.orig_UpdateSliders orig, UIWorldCreation self) { }
        /*
        public void ShowOptionDescription(OnUIWorldCreation.orig_ShowOptionDescription orig, UIWorldCreation self, UIMouseEvent evt, UIElement listeningElement) {
            if (listeningElement is CustomGroupButton<Tuple<int, string, string>>)
            {
                CustomGroupButton<Tuple<int, string, string>> customGroupButton = (CustomGroupButton<Tuple<int, string, string>>)listeningElement;

                UIState currentScreen = UserInterface.ActiveInstance.CurrentState;
                UIWorldCreation creationScreen = (UIWorldCreation)currentScreen;
                BindingFlags yes = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                PropertyInfo prop = creationScreen.GetType().GetProperty("_descriptionText", yes);
                UIText desc = (UIText)prop.GetValue(creationScreen);
                desc.SetText(customGroupButton.Description);


                /*
                Type uiType = self.GetType();
                PropertyInfo info = uiType.GetProperty("_descriptionText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                UIText val = info.GetValue(self) as UIText;
                val.SetText(groupOptionButton2.Description);
                

            }
            orig(self, evt, listeningElement);
        }
        */
        
        public void ShowOptionDescription(UIMouseEvent evt, UIElement listeningElement)
        {
            if (listeningElement is CustomGroupButton<Tuple<int, string, string>>)
            {
                CustomGroupButton<Tuple<int, string, string>> customGroupButton = (CustomGroupButton<Tuple<int, string, string>>)listeningElement;

                UIState currentScreen = UserInterface.ActiveInstance.CurrentState;
                UIWorldCreation creationScreen = (UIWorldCreation)currentScreen;
                BindingFlags yes = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                FieldInfo prop = creationScreen.GetType().GetField("_descriptionText", yes);
                UIText desc = (UIText)prop.GetValue(creationScreen);
                desc.SetText(customGroupButton.Description);
            }
        }
        
        public System.Threading.Tasks.Task CreateNewWorld(OnWorldGen.orig_CreateNewWorld orig, GenerationProgress progress)
        {
            CurrentDifficulty = worldCreationDifficultyValue;
            Main.GameMode = CurrentDifficulty.Item1;
            if (CurrentDifficulty.Item1 == 4)
            {
                Main.RegisteredGameModes.Remove(4);
                Main.RegisteredGameModes.Add(4, GetGameMode(worldCreationDifficultyValue.Item2, worldCreationDifficultyValue.Item3).GameModeData);
            }
            return orig(progress);
        }

        public void Click_GoBack(OnUIWorldCreation.orig_Click_GoBack orig, UIWorldCreation self, UIMouseEvent evt, UIElement listeningElement) {
            if (difficultyButtons != null) 
             difficultyButtons.Clear();
            fakeContainerLOL.RemoveAllChildren();
            fakeContainerLOL.Remove();
            fakeContainerLOL = null;
            Mod.Logger.Info(body);
            orig(self,evt, listeningElement);
        }


        //UIWorldCreationPreview
        public void DrawPreviewPlate(OnUIWorldCreationPreview.orig_DrawSelf orig, UIWorldCreationPreview self, SpriteBatch spriteBatch)
        {
            Texture2D _BorderTexture = (Texture2D) Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewBorder");
            Texture2D _BackgroundNormalTexture = (Texture2D) Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyNormal1");
            Texture2D _BackgroundExpertTexture = (Texture2D) Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyExpert1");
            Texture2D _BackgroundMasterTexture = (Texture2D) Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyMaster1");
            Texture2D _BunnyNormalTexture = (Texture2D) Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyNormal2");
            Texture2D _BunnyExpertTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyExpert2");
            Texture2D _BunnyCreativeTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyCreative2");
            Texture2D _BunnyMasterTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewDifficultyMaster2");
            Texture2D _EvilRandomTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewEvilRandom");
            Texture2D _EvilCorruptionTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewEvilCorruption");
            Texture2D _EvilCrimsonTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewEvilCrimson");
            Texture2D _SizeSmallTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewSizeSmall");
            Texture2D _SizeMediumTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewSizeMedium");
            Texture2D _SizeLargeTexture = (Texture2D)Main.Assets.Request<Texture2D>("Images/UI/WorldCreation/PreviewSizeLarge");
            CalculatedStyle dimensions = self.GetDimensions();
            Vector2 position = new Vector2(dimensions.X + 4f, dimensions.Y + 4f);
            Color color = Color.White;


            BindingFlags yes = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            byte _evil = (byte)self.GetType().GetField("_evil", yes).GetValue(self);
            byte _size = (byte)self.GetType().GetField("_size", yes).GetValue(self);

            switch (worldCreationDifficultyValue.Item1)
            {
                case 0:
                case 3:
                    spriteBatch.Draw(_BackgroundNormalTexture, position, Color.White);
                    color = Color.White;
                    break;
                case 1:
                    spriteBatch.Draw(_BackgroundExpertTexture, position, Color.White);
                    color = Color.DarkGray;
                    break;
                case 2:
                    spriteBatch.Draw(_BackgroundMasterTexture, position, Color.White);
                    color = Color.DarkGray;
                    break;
                case 4:

                    ModDifficulty dif = GameModeSystem.Instance.GetGameMode(worldCreationDifficultyValue.Item2, worldCreationDifficultyValue.Item3);

                    if (dif == null)
                    {
                        spriteBatch.Draw(_BackgroundNormalTexture, position, Color.White);
                        color = Color.White;
                        break;
                    }

                    spriteBatch.Draw(dif.WorldCreationPreviewSky.Value, position, Color.White);
                    color = dif.WorldCreationPreviewTint;
                    break;
            }

            switch (_size)
            {
                case 0:
                    spriteBatch.Draw(_SizeSmallTexture, position, color);
                    break;
                case 1:
                    spriteBatch.Draw(_SizeMediumTexture, position, color);
                    break;
                case 2:
                    spriteBatch.Draw(_SizeLargeTexture, position, color);
                    break;
            }

            switch (_evil)
            {
                case 0:
                    spriteBatch.Draw(_EvilRandomTexture, position, color);
                    break;
                case 1:
                    spriteBatch.Draw(_EvilCorruptionTexture, position, color);
                    break;
                case 2:
                    spriteBatch.Draw(_EvilCrimsonTexture, position, color);
                    break;
            }

            switch (worldCreationDifficultyValue.Item1)
            {
                case 0:
                    spriteBatch.Draw(_BunnyNormalTexture, position, color);
                    break;
                case 1:
                    spriteBatch.Draw(_BunnyExpertTexture, position, color);
                    break;
                case 2:
                    spriteBatch.Draw(_BunnyMasterTexture, position, color * 1.2f);
                    break;
                case 3:
                    spriteBatch.Draw(_BunnyCreativeTexture, position, color);
                    break;
                case 4:

                    ModDifficulty dif = GameModeSystem.Instance.GetGameMode(worldCreationDifficultyValue.Item2, worldCreationDifficultyValue.Item3);

                    if (dif == null)
                    {
                        spriteBatch.Draw(_BunnyCreativeTexture, position, color);
                        break;
                    }

                    spriteBatch.Draw(dif.WorldCreationPreviewBunny.Value, position, color);
                    break;
            }

            spriteBatch.Draw(_BorderTexture, new Vector2(dimensions.X, dimensions.Y), Color.White);
        }




    }
}
