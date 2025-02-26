using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using SFD;
using SFD.Colors;
using SFD.GUI.Text;
using SFD.Parser;
using SFD.Sounds;
using SFD.States;
using SFD.Tiles;
using SFD.UserProgression;
using SFR.Fighter;
using SFR.Helper;

namespace SFR.Bootstrap;

/// <summary>
///     This is where SFR starts.
///     This class handles and loads all the new textures, music, sounds, tiles, colors etc...
///     This class is also used to tweak some game code on startup, such as window title.
/// </summary>
[HarmonyPatch]
internal static class Assets
{
    private static readonly string ContentPath = Path.Combine(Program.GameDirectory, @"SFR\Content");
    private static readonly string OfficialsMapsPath = Path.Combine(ContentPath, @"Data\Maps\Official");

    /// <summary>
    ///     Some items like Armband are locked by default.
    ///     Here we unlock all items & prevent specific ones from being equipped.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Challenges), nameof(Challenges.Load))]
    private static void UnlockItems()
    {
        Items.GetAllItems(Item.GenderType.Unisex).ForEach(i =>
        {
            i.CanScript = true;
            i.CanEquip = true;
            i.Locked = false;
        });

        Items.GetItems("Burnt", "Burnt_fem", "BearSkin", "MechSkin", "FrankenbearSkin", "NormalHeadless", "NormalHeadless_fem", "BurntHeadless", "BurntHeadless_fem", "ZombieHeadless", "ZombieHeadless_fem", "TattoosHeadless", "TattoosHeadless_fem", "WarpaintHeadless", "WarpaintHeadless_fem", "ExposedBrain", "Headless", "HeadShot", "HeadShot2", "HeadShot3", "ChestCavity").ForEach(i => i.Locked = true);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Constants), nameof(Constants.SetupTextures))]
    private static void LoadAdditionalTeamTextures()
    {
        Misc.Constants.TeamIcon5 = Textures.GetTexture("TeamIcon5");
        Misc.Constants.TeamIcon6 = Textures.GetTexture("TeamIcon6");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MusicHandler), nameof(MusicHandler.Initialize))]
    private static void LoadMusic()
    {
        Logger.LogInfo("LOADING: Music");
        MusicHandler.m_trackPaths.Add((MusicHandler.MusicTrackID)42, Path.Combine(ContentPath, @"Data\Sounds\Music\Metrolaw.mp3"));
        MusicHandler.m_trackPaths.Add((MusicHandler.MusicTrackID)43, Path.Combine(ContentPath, @"Data\Sounds\Music\FrozenBlood.mp3"));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MusicHandler), nameof(MusicHandler.PlayTitleTrack))]
    private static bool PlayTitleMusic()
    {
        MusicHandler.PlayTrack((MusicHandler.MusicTrackID)42);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SoundHandler), nameof(SoundHandler.Load))]
    private static void LoadSounds(GameSFD game)
    {
        Logger.LogInfo("LOADING: Sounds");
        foreach (string data in Directory.GetFiles(Path.Combine(ContentPath, @"Data\Sounds"), "*.sfds"))
        {
            var soundsData = SFDSimpleReader.Read(data);
            foreach (string soundData in soundsData)
            {
                string[] soundFields = SFDSimpleReader.Interpret(soundData).ToArray();
                if (soundFields.Length < 3)
                {
                    continue;
                }

                var sound = new SoundEffect[soundFields.Length - 2];
                float pitch = SFDXParser.ParseFloat(soundFields[1]);

                for (int i = 0; i < sound.Length; i++)
                {
                    string loadPath = Path.Combine(ContentPath, @"Data\Sounds", soundFields[i + 2]);
                    sound[i] = game.Content.Load<SoundEffect>(loadPath);
                }

                int count = sound.Count(t => t == null);

                if (count > 0)
                {
                    var extraSounds = new SoundEffect[sound.Length - count];
                    int field = 0;
                    foreach (var soundEffect in sound)
                    {
                        if (soundEffect != null)
                        {
                            extraSounds[field] = soundEffect;
                            field++;
                        }
                    }

                    sound = extraSounds;
                }

                SoundHandler.SoundEffectGroup finalSound = new(soundFields[0], pitch, sound);
                SoundHandler.soundEffects.Add(finalSound);
            }
        }
    }

    /// <summary>
    ///     This method is executed whenever we close the game or it crash.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSFD), nameof(GameSFD.OnExiting))]
    private static void Dispose()
    {
        Logger.LogError("Disposing");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Constants), nameof(Constants.Load))]
    private static void LoadFonts()
    {
        Logger.LogInfo("LOADING: Fonts");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Constants), nameof(Constants.Load))]
    private static IEnumerable<CodeInstruction> LoadFonts(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.operand == null)
            {
                continue;
            }

            if (instruction.operand.Equals("Data\\Fonts\\"))
            {
                instruction.operand = Path.Combine(ContentPath, @"Data\Fonts");
            }
        }

        return instructions;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StateLoading), nameof(StateLoading.Load), new[] { typeof(LoadState) }, new[] { ArgumentType.Ref })]
    private static void LoadAdditionTeamIconChat(ref LoadState loadingState, StateLoading __instance)
    {
        if (TextIcons.m_icons != null)
        {
            TextIcons.m_icons.Remove("TEAM_5");
            TextIcons.Add("TEAM_5", Textures.GetTexture("TeamIcon5"));
            TextIcons.Add("TEAM_6", Textures.GetTexture("TeamIcon6"));
            TextIcons.Add("TEAM_S", Textures.GetTexture("TeamIconS"));
        }
    }

    /// <summary>
    ///     Fix for loading SFR and SFD textures from both paths.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TitleContainer), nameof(TitleContainer.OpenStream))]
    private static bool StreamPatch(string name, ref Stream __result)
    {
        if (name.Contains(@"Content\Data"))
        {
            if (name.EndsWith(".xnb.xnb"))
            {
                name = name.Substring(0, name.Length - 4);
            }

            __result = File.OpenRead(name);
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TileDatabase), nameof(TileDatabase.Load))]
    private static void LoadTiles()
    {
        Logger.LogInfo("LOADING: Tiles");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(TileDatabase), nameof(TileDatabase.Load))]
    private static IEnumerable<CodeInstruction> LoadTiles(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.operand == null)
            {
                continue;
            }

            if (instruction.operand.Equals("Data\\Tiles\\"))
            {
                instruction.operand = Path.Combine(ContentPath, @"Data\Tiles");
            }
            else if (instruction.operand.Equals("Data\\Weapons\\"))
            {
                instruction.operand = Path.Combine(ContentPath, @"Data\Weapons");
                break;
            }
        }

        return instructions;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ColorDatabase), nameof(ColorDatabase.Load))]
    private static bool LoadColors(GameSFD game)
    {
        Logger.LogInfo("LOADING: Colors");
        ColorDatabase.LoadColors(game, Path.Combine(ContentPath, @"Data\Colors\Colors"));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ColorPaletteDatabase), nameof(ColorDatabase.Load))]
    private static bool LoadColorsPalette(GameSFD game)
    {
        Logger.LogInfo("LOADING: Palettes");
        ColorPaletteDatabase.LoadColorPalettes(game, Path.Combine(ContentPath, @"Data\Colors\Palettes"));
        return false;
    }

    /// <summary>
    ///     Load SFR maps into the officials category.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapHandler), nameof(MapHandler.ReadMapInfoFromStorages), new Type[] { })]
    private static bool LoadMaps(ref List<MapInfo> __result)
    {
        Logger.LogInfo("LOADING: Maps");
        Constants.SetThreadCultureInfo(Thread.CurrentThread);
        var list = new List<MapInfo>();
        string[] array =
        {
            Constants.Paths.ContentOfficialMapsPath,
            Constants.Paths.UserDocumentsCustomMapsPath,
            Constants.Paths.UserDocumentsDownloadedMapsPath,
            Constants.Paths.ContentCustomMapsPath,
            Constants.Paths.ContentDownloadedMapsPath,
            OfficialsMapsPath
        };
        var loadedMaps = new HashSet<Guid>();
        foreach (string t in array)
        {
            MapHandler.ReadMapInfoFromStorages(list, t, loadedMaps, true);
        }

        if (!string.IsNullOrEmpty(Constants.STEAM_WORKSHOP_FOLDER))
        {
            MapHandler.ReadMapInfoFromStorages(list, Constants.STEAM_WORKSHOP_FOLDER, loadedMaps, false);
        }

        __result = list.OrderBy(m => m.Name).ToList();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapInfo), nameof(MapInfo.SetFilePathData))]
    private static bool LoadMaps(string pathToFile, MapInfo __instance)
    {
        if (string.IsNullOrEmpty(pathToFile))
        {
            __instance.Folder = "Other";
            return false;
        }

        __instance.FullPathToFile = Path.GetFullPath(pathToFile);
        __instance.FileName = Path.GetFileName(__instance.FullPathToFile);
        string directoryName = Path.GetDirectoryName(__instance.FullPathToFile);
        __instance.SaveDate = DateTime.MinValue;
        __instance.IsSteamSubscription = !string.IsNullOrEmpty(Constants.STEAM_WORKSHOP_FOLDER) && pathToFile.StartsWith(Constants.STEAM_WORKSHOP_FOLDER, StringComparison.InvariantCultureIgnoreCase);
        if (directoryName!.StartsWith(OfficialsMapsPath, true, null))
        {
            __instance.Folder = "Official" + directoryName.Substring(OfficialsMapsPath.Length);
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Textures), nameof(Textures.Load), new Type[] { })]
    private static void LoadTextures()
    {
        Logger.LogInfo("LOADING: Textures");
        Textures.Load(Path.Combine(ContentPath, @"Data\Images"));
    }

    /// <summary>
    ///     Fix for loading SFR and SFD textures from both paths.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Constants.Paths), nameof(Constants.Paths.GetContentAssetPathFromFullPath))]
    private static bool GetContentAssetPathFromFullPath(string path, ref string __result)
    {
        __result = path;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Items), nameof(Items.Load))]
    private static bool LoadItems(GameSFD game)
    {
        Logger.LogInfo("LOADING: Items");

        var content = game.Content;
        Items.m_allItems = new List<Item>();
        Items.m_allFemaleItems = new List<Item>();
        Items.m_allMaleItems = new List<Item>();
        Items.m_slotAllItems = new List<Item>[10];
        Items.m_slotFemaleItems = new List<Item>[10];
        Items.m_slotMaleItems = new List<Item>[10];

        for (int i = 0; i < Items.m_slotAllItems.Length; i++)
        {
            Items.m_slotAllItems[i] = new List<Item>();
            Items.m_slotFemaleItems[i] = new List<Item>();
            Items.m_slotMaleItems[i] = new List<Item>();
        }

        var files = Directory.GetFiles(Path.Combine(ContentPath, @"Data\Items"), "*.xnb", SearchOption.AllDirectories).ToList();
        var originalItems = Directory.GetFiles(Constants.Paths.GetContentFullPath(@"Data\Items"), "*.xnb", SearchOption.AllDirectories).ToList();
        foreach (string item in originalItems)
        {
            if (files.TrueForAll(f => Path.GetFileNameWithoutExtension(f) != Path.GetFileNameWithoutExtension(item)))
            {
                files.Add(item);
            }
        }

        foreach (string file in files)
        {
            if (GameSFD.Closing)
            {
                return false;
            }

            var item = content.Load<Item>(file);
            if (Items.m_allItems.Any(item2 => item2.ID == item.ID))
            {
                throw new Exception("Can't load items");
            }

            item.PostProcess();
            Items.m_allItems.Add(item);
            Items.m_slotAllItems[item.EquipmentLayer].Add(item);
        }

        Items.PostProcessGenders();
        Player.HurtLevel1 = Items.GetItem("HurtLevel1");
        Player.HurtLevel2 = Items.GetItem("HurtLevel2") ?? Player.HurtLevel1;

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameSFD), MethodType.Constructor, new Type[] { })]
    private static void Init(GameSFD __instance)
    {
        __instance.Window.Title = $"Superfighters Redux {Misc.Constants.SFRVersion}";
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Animations), nameof(Animations.Load))]
    private static bool LoadCustomAnimations(ref bool __result)
    {
        Logger.LogInfo("LOADING: Custom Animations");
        Animations.Data = Animations.LoadAnimationsDataPipeline(Path.Combine(ContentPath, @"Data\Animations"));
        __result = true;

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Animations), nameof(Animations.Load))]
    private static void EditAnimations(Microsoft.Xna.Framework.Game game)
    {
        var data = Animations.Data;
        var anims = data.Animations;

        var customData = AnimHandler.GetAnimations(data);
        Array.Resize(ref anims, data.Animations.Length + customData.Count);
        for (int i = 0; i < customData.Count; i++)
        {
            anims[anims.Length - 1 - i] = customData[i];
            // Logger.LogDebug("Adding animation: " + customData[i].Name);
        }

        data.Animations = anims;
        AnimationsData animData = new(data.Animations);
        Animations.Data = animData;
    }
}