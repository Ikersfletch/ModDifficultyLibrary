using System;
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
using Terraria.GameContent.UI.States;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI.Gamepad;

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
            OnUI.SetState += ModifySetState;
        }
        public override void Unload()
        {
            Main.RegisteredGameModes.Remove(4);
            OnWorldItem.DrawSelf -= DrawWorldSelectItem;
            OnUI.SetState -= ModifySetState;
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
    }
}
