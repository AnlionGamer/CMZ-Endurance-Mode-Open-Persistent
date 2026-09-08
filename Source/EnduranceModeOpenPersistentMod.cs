using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using CMZ.ModSDK;
using HarmonyLib;


namespace CMZ.EnduranceModeOpenPersistent
{
    public sealed class EnduranceModeOpenPersistentMod : ICMZMod
    {
        private const string HudTypeName = "DNA.CastleMinerZ.UI.InGameHUD";
        private const string HudGateFieldName = "gameBegun";
        private const string HudUpdateMethodName = "OnUpdate";

        private const string FrontEndTypeName = "DNA.CastleMinerZ.FrontEndScreen";
        private const string GameModeMenuMethodName = "_gameModeMenu_MenuItemSelected";
        private const string GameFieldName = "_game";
        private const string UiGroupFieldName = "_uiGroup";
        private const string ChooseSavedWorldFieldName = "_chooseSavedWorldScreen";

        private const string GameTypeName = "DNA.CastleMinerZ.CastleMinerZGame";
        private const string GameModeFieldName = "GameMode";
        private const string CurrentWorldFieldName = "CurrentWorld";
        private const string SaveDataInternalMethodName = "SaveDataInternal";
        private const string EndGameMethodName = "EndGame";

        private const string WorldManagerTypeName = "DNA.CastleMinerZ.WorldManager";
        private const string WorldManagerDeleteMethodName = "Delete";

        private static IModContext _context;
        private static int _gateObserved;
        private static int _menuRouteObserved;
        private static int _persistentSaveObserved;
        private static int _autoDeleteSuppressedObserved;
        private static FieldInfo _gameModeField;
        private static FieldInfo _currentWorldField;

        [ThreadStatic]
        private static bool _insideEnduranceEndGame;

        [ThreadStatic]
        private static object _enduranceWorldBeingEnded;

        public void OnLoad(IModContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            _context = context;
            context.Log.Info(ModBuildIdentity.DisplayName + " v" + ModBuildIdentity.Version + " loading.");

            if (!InstallLateJoinPatch(context))
                return;

            if (!InstallEnduranceWorldChooserPatch(context))
                return;

            if (!InstallPersistentWorldSavePatch(context))
                return;

            if (!InstallEnduranceWorldRetentionPatch(context))
                return;

            context.Log.Info(ModBuildIdentity.DisplayName + " v" + ModBuildIdentity.Version + " patches installed successfully.");
        }

        public void OnGameReady()
        {
            IModContext context = _context;
            if (context != null)
            {
                context.Log.Info(
                    ModBuildIdentity.DisplayName + " is ready. Normal Endurance remains Endurance mechanically, while world selection, saving, retention and returning-player inventory persistence follow the vanilla persistent-world infrastructure.");
            }
        }

        public void OnShutdown()
        {
            IModContext context = _context;
            if (context != null)
                context.Log.Info(ModBuildIdentity.DisplayName + " shutting down.");

            _context = null;
            Interlocked.Exchange(ref _gateObserved, 0);
            Interlocked.Exchange(ref _menuRouteObserved, 0);
            Interlocked.Exchange(ref _persistentSaveObserved, 0);
            Interlocked.Exchange(ref _autoDeleteSuppressedObserved, 0);
            _gameModeField = null;
            _currentWorldField = null;
            _insideEnduranceEndGame = false;
            _enduranceWorldBeingEnded = null;
        }

        private static bool InstallLateJoinPatch(IModContext context)
        {
            Type hudType = FindLoadedType(HudTypeName);
            if (hudType == null)
            {
                context.Log.Error("Target type was not found: " + HudTypeName + ". No patches were installed.");
                return false;
            }

            FieldInfo gameBegun = FindInstanceField(hudType, HudGateFieldName);
            if (gameBegun == null || gameBegun.FieldType != typeof(bool))
            {
                context.Log.Error("Expected Boolean field " + HudTypeName + "." + HudGateFieldName + " was not found. No patches were installed.");
                return false;
            }

            MethodInfo target = FindSingleDeclaredMethod(hudType, HudUpdateMethodName);
            if (target == null)
            {
                context.Log.Error("Expected unique declared method " + HudTypeName + "." + HudUpdateMethodName + " was not found. No patches were installed.");
                return false;
            }

            MethodInfo prefix = typeof(EnduranceModeOpenPersistentMod).GetMethod(
                "BeforeInGameHudOnUpdate",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (prefix == null)
            {
                context.Log.Error("Internal Endurance late-join prefix was not found. No patches were installed.");
                return false;
            }

            context.Patches.Prefix(target, prefix);
            context.Log.Info(
                "Installed proven v1.0.0 Endurance late-join gate patch on " +
                target.DeclaringType.FullName + "." + target.Name +
                "; owns=" + context.Patches.Owns(target) + ".");
            return true;
        }

        private static bool InstallEnduranceWorldChooserPatch(IModContext context)
        {
            Type frontEndType = FindLoadedType(FrontEndTypeName);
            if (frontEndType == null)
            {
                context.Log.Error("Target type was not found: " + FrontEndTypeName + ". Endurance world chooser patch was not installed.");
                return false;
            }

            if (FindInstanceField(frontEndType, GameFieldName) == null ||
                FindInstanceField(frontEndType, UiGroupFieldName) == null ||
                FindInstanceField(frontEndType, ChooseSavedWorldFieldName) == null)
            {
                context.Log.Error("Expected FrontEndScreen fields for the vanilla saved-world routing were not found. Endurance world chooser patch was not installed.");
                return false;
            }

            MethodInfo target = FindSingleDeclaredMethod(frontEndType, GameModeMenuMethodName);
            if (target == null)
            {
                context.Log.Error("Expected unique declared method " + FrontEndTypeName + "." + GameModeMenuMethodName + " was not found. Endurance world chooser patch was not installed.");
                return false;
            }

            ParameterInfo[] parameters = target.GetParameters();
            if (parameters.Length != 2)
            {
                context.Log.Error("Unexpected game-mode menu handler signature. Expected exactly two parameters; found " + parameters.Length + ". Endurance world chooser patch was not installed.");
                return false;
            }

            MethodInfo prefix = typeof(EnduranceModeOpenPersistentMod).GetMethod(
                "BeforeGameModeMenuItemSelected",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (prefix == null)
            {
                context.Log.Error("Internal Endurance world chooser prefix was not found. Endurance world chooser patch was not installed.");
                return false;
            }

            context.Patches.Prefix(target, prefix);
            context.Log.Info(
                "Installed Endurance world/server chooser routing patch on " +
                target.DeclaringType.FullName + "." + target.Name +
                "; owns=" + context.Patches.Owns(target) + ".");
            return true;
        }

        private static bool InstallPersistentWorldSavePatch(IModContext context)
        {
            Type gameType = FindLoadedType(GameTypeName);
            if (gameType == null)
            {
                context.Log.Error("Target type was not found: " + GameTypeName + ". Persistent Endurance save patch was not installed.");
                return false;
            }

            FieldInfo gameModeField = FindInstanceField(gameType, GameModeFieldName);
            if (gameModeField == null)
            {
                context.Log.Error("Expected field " + GameTypeName + "." + GameModeFieldName + " was not found. Persistent Endurance save patch was not installed.");
                return false;
            }

            MethodInfo target = FindSingleDeclaredMethod(gameType, SaveDataInternalMethodName);
            if (target == null)
            {
                context.Log.Error("Expected unique declared method " + GameTypeName + "." + SaveDataInternalMethodName + " was not found. Persistent Endurance save patch was not installed.");
                return false;
            }

            MethodInfo transpiler = typeof(EnduranceModeOpenPersistentMod).GetMethod(
                "PersistentWorldSaveTranspiler",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (transpiler == null)
            {
                context.Log.Error("Internal persistent-world save transpiler was not found. Persistent Endurance save patch was not installed.");
                return false;
            }

            _gameModeField = gameModeField;
            context.Patches.Transpiler(target, transpiler);
            context.Log.Info(
                "Installed persistent-world save patch on " +
                target.DeclaringType.FullName + "." + target.Name +
                "; normal Endurance now executes the same blocking ChunkCache.Flush(true) path used by persistent modes; owns=" +
                context.Patches.Owns(target) + ".");
            return true;
        }

        private static bool InstallEnduranceWorldRetentionPatch(IModContext context)
        {
            Type gameType = FindLoadedType(GameTypeName);
            Type worldManagerType = FindLoadedType(WorldManagerTypeName);
            if (gameType == null || worldManagerType == null)
            {
                context.Log.Error("Endurance world-retention target types were not found. Automatic Endurance deletion suppression was not installed.");
                return false;
            }

            FieldInfo gameModeField = FindInstanceField(gameType, GameModeFieldName);
            FieldInfo currentWorldField = FindInstanceField(gameType, CurrentWorldFieldName);
            if (gameModeField == null || currentWorldField == null)
            {
                context.Log.Error("Expected CastleMinerZGame GameMode/CurrentWorld fields were not found. Automatic Endurance deletion suppression was not installed.");
                return false;
            }

            MethodInfo endGame = FindSingleDeclaredMethod(gameType, EndGameMethodName);
            MethodInfo deleteWorld = FindSingleDeclaredMethod(worldManagerType, WorldManagerDeleteMethodName);
            if (endGame == null || deleteWorld == null)
            {
                context.Log.Error("Expected EndGame/WorldManager.Delete methods were not found uniquely. Automatic Endurance deletion suppression was not installed.");
                return false;
            }

            ParameterInfo[] deleteParameters = deleteWorld.GetParameters();
            if (deleteParameters.Length != 1)
            {
                context.Log.Error("Unexpected WorldManager.Delete signature. Expected one world parameter; found " + deleteParameters.Length + ".");
                return false;
            }

            MethodInfo endGamePrefix = typeof(EnduranceModeOpenPersistentMod).GetMethod(
                "BeforeEndGame",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo endGameFinalizer = typeof(EnduranceModeOpenPersistentMod).GetMethod(
                "AfterEndGameFinalizer",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo deletePrefix = typeof(EnduranceModeOpenPersistentMod).GetMethod(
                "BeforeWorldManagerDelete",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (endGamePrefix == null || endGameFinalizer == null || deletePrefix == null)
            {
                context.Log.Error("Internal Endurance world-retention patch methods were not found.");
                return false;
            }

            _gameModeField = gameModeField;
            _currentWorldField = currentWorldField;

            context.Patches.Prefix(endGame, endGamePrefix);
            context.Patches.Finalizer(endGame, endGameFinalizer);
            context.Patches.Prefix(deleteWorld, deletePrefix);

            context.Log.Info(
                "Installed Endurance world-retention guard across " +
                endGame.DeclaringType.FullName + "." + endGame.Name +
                " and " + deleteWorld.DeclaringType.FullName + "." + deleteWorld.Name +
                ". Explicit Delete World actions remain vanilla; only EndGame's automatic normal-Endurance deletion is suppressed.");
            return true;
        }

        // Vanilla SaveDataInternal contains exactly one GameMode-dependent persistence
        // branch: normal Endurance (numeric 0) skips ChunkCache.Flush(true), while
        // every nonzero game mode executes it. Replacing that conditional branch with
        // Pop consumes the already-loaded GameMode value and falls through to the
        // unmodified vanilla flush instructions. Other modes therefore remain bytecode-
        // equivalent in effect, while normal Endurance gains persistent-mode terrain saving.
        private static IEnumerable<CodeInstruction> PersistentWorldSaveTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            FieldInfo gameModeField = _gameModeField;
            bool changed = false;

            if (gameModeField == null)
                throw new InvalidOperationException("GameMode field was not initialized before persistent-world save transpilation.");

            for (int i = 0; i + 1 < list.Count; i++)
            {
                CodeInstruction load = list[i];
                CodeInstruction branch = list[i + 1];

                if (load.opcode != OpCodes.Ldfld || !object.Equals(load.operand, gameModeField))
                    continue;

                if (branch.opcode != OpCodes.Brfalse && branch.opcode != OpCodes.Brfalse_S)
                    continue;

                branch.opcode = OpCodes.Pop;
                branch.operand = null;
                changed = true;
                break;
            }

            if (!changed)
                throw new InvalidOperationException("Trusted SaveDataInternal Endurance chunk-flush branch was not found; refusing an unsafe persistence rewrite.");

            if (Interlocked.Exchange(ref _persistentSaveObserved, 1) == 0)
            {
                IModContext context = _context;
                if (context != null)
                    context.Log.Info("Normal Endurance save-time chunk-flush exception removed; vanilla persistent-mode terrain flush path is active.");
            }

            return list;
        }

        // EndGame's normal-Endurance-only cleanup calls WorldManager.Delete(CurrentWorld).
        // Persistent modes skip that block. We mark only the synchronous EndGame scope,
        // remember the exact world being ended, and suppress only that matching Delete.
        // Manual Delete World actions occur outside this scope and remain untouched.
        private static void BeforeEndGame(object __instance)
        {
            _insideEnduranceEndGame = false;
            _enduranceWorldBeingEnded = null;

            if (__instance == null || _gameModeField == null || _currentWorldField == null)
                return;

            object mode = _gameModeField.GetValue(__instance);
            if (mode == null || Convert.ToInt32(mode) != 0)
                return;

            object currentWorld = _currentWorldField.GetValue(__instance);
            if (currentWorld == null)
                return;

            _insideEnduranceEndGame = true;
            _enduranceWorldBeingEnded = currentWorld;
        }

        private static bool BeforeWorldManagerDelete(object __0)
        {
            if (!_insideEnduranceEndGame || __0 == null || !object.ReferenceEquals(__0, _enduranceWorldBeingEnded))
                return true;

            if (Interlocked.Exchange(ref _autoDeleteSuppressedObserved, 1) == 0)
            {
                IModContext context = _context;
                if (context != null)
                    context.Log.Info("Suppressed vanilla EndGame automatic deletion of the normal Endurance world. The world remains available for later sessions.");
            }

            return false;
        }

        private static Exception AfterEndGameFinalizer(Exception __exception)
        {
            _insideEnduranceEndGame = false;
            _enduranceWorldBeingEnded = null;
            return __exception;
        }

        // This is the v1.0.0 runtime-tested late-join behavior, intentionally kept
        // unchanged: Harmony private-field injection maps ___gameBegun to
        // InGameHUD.gameBegun. Vanilla OnUpdate uses this Boolean as the one-shot
        // gate around the normal-Endurance distance/day lockout publication.
        private static void BeforeInGameHudOnUpdate(ref bool ___gameBegun)
        {
            if (!___gameBegun)
            {
                ___gameBegun = true;

                if (Interlocked.Exchange(ref _gateObserved, 1) == 0)
                {
                    IModContext context = _context;
                    if (context != null)
                        context.Log.Info("Vanilla Endurance late-join gate suppression is active for this in-game HUD instance.");
                }
            }
        }

        // FrontEndScreen's vanilla handler sends GameModeTypes.Endurance (numeric 0)
        // directly to startWorld() in both the local/offline and hosted-online branches.
        // This prefix intercepts only that one selection, reproduces the common vanilla
        // state initialization that occurs before the branch, then pushes CMZ's existing
        // ChooseSavedWorldScreen. All other game modes and Back fall through unchanged.
        private static bool BeforeGameModeMenuItemSelected(object __instance, object __1)
        {
            try
            {
                if (__instance == null || __1 == null)
                    return true;

                object menuItem = GetMemberValue(__1, "MenuItem");
                if (menuItem == null)
                    return true;

                object tag = GetMemberValue(menuItem, "Tag");
                if (tag == null)
                    return true;

                int selectedMode;
                try
                {
                    selectedMode = Convert.ToInt32(tag);
                }
                catch
                {
                    return true;
                }

                // GameModeTypes.Endurance == 0 in the trusted CMZ 1.9.9.8 baseline.
                if (selectedMode != 0)
                    return true;

                object game = GetRequiredFieldValue(__instance, GameFieldName);
                object uiGroup = GetRequiredFieldValue(__instance, UiGroupFieldName);
                object chooseSavedWorldScreen = GetRequiredFieldValue(__instance, ChooseSavedWorldFieldName);

                // Match the common vanilla initialization performed before the local/
                // online switch in _gameModeMenu_MenuItemSelected.
                SetRequiredFieldValue(game, "GameMode", 0);
                SetRequiredFieldValue(game, "InfiniteResourceMode", false);
                SetRequiredFieldValue(game, "Difficulty", 0);
                SetRequiredPropertyValue(game, "JoinGamePolicy", 0);

                InvokePushScreen(uiGroup, chooseSavedWorldScreen);

                if (Interlocked.Exchange(ref _menuRouteObserved, 1) == 0)
                {
                    IModContext context = _context;
                    if (context != null)
                    {
                        context.Log.Info(
                            "Normal Endurance selection routed to the vanilla Choose A Server screen; difficulty selection remains bypassed.");
                    }
                }

                // Skip only vanilla's Endurance branch, which would otherwise call
                // startWorld() immediately. ChooseSavedWorldScreen's existing buttons
                // retain vanilla New World, existing-world, delete, rename and save logic.
                return false;
            }
            catch (Exception ex)
            {
                IModContext context = _context;
                if (context != null)
                    context.Log.Error("Endurance Choose A Server routing failed safely; allowing vanilla handler to continue. " + ex.GetType().Name + ": " + ex.Message);

                return true;
            }
        }

        private static object GetRequiredFieldValue(object instance, string fieldName)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");

            FieldInfo field = FindInstanceField(instance.GetType(), fieldName);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, fieldName);

            object value = field.GetValue(instance);
            if (value == null)
                throw new InvalidOperationException(instance.GetType().FullName + "." + fieldName + " is null.");

            return value;
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
                return null;

            FieldInfo field = FindInstanceField(instance.GetType(), memberName);
            if (field != null)
                return field.GetValue(instance);

            PropertyInfo property = FindInstanceProperty(instance.GetType(), memberName);
            if (property != null && property.CanRead)
                return property.GetValue(instance, null);

            return null;
        }

        private static void SetRequiredFieldValue(object instance, string fieldName, object value)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");

            FieldInfo field = FindInstanceField(instance.GetType(), fieldName);
            if (field == null)
                throw new MissingFieldException(instance.GetType().FullName, fieldName);

            field.SetValue(instance, ConvertForTargetType(value, field.FieldType));
        }

        private static void SetRequiredPropertyValue(object instance, string propertyName, object value)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");

            PropertyInfo property = FindInstanceProperty(instance.GetType(), propertyName);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(instance.GetType().FullName, propertyName);

            property.SetValue(instance, ConvertForTargetType(value, property.PropertyType), null);
        }

        private static object ConvertForTargetType(object value, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException("targetType");

            if (value == null)
                return null;

            if (targetType.IsInstanceOfType(value))
                return value;

            if (targetType.IsEnum)
            {
                int numeric = Convert.ToInt32(value);
                return Enum.ToObject(targetType, numeric);
            }

            return Convert.ChangeType(value, targetType);
        }

        private static void InvokePushScreen(object uiGroup, object screen)
        {
            if (uiGroup == null)
                throw new ArgumentNullException("uiGroup");
            if (screen == null)
                throw new ArgumentNullException("screen");

            MethodInfo push = FindCompatibleSingleParameterMethod(uiGroup.GetType(), "PushScreen", screen.GetType());
            if (push == null)
                throw new MissingMethodException(uiGroup.GetType().FullName, "PushScreen");

            push.Invoke(uiGroup, new object[] { screen });
        }

        private static FieldInfo FindInstanceField(Type type, string fieldName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                    return field;
            }

            return null;
        }

        private static PropertyInfo FindInstanceProperty(Type type, string propertyName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (property != null)
                    return property;
            }

            return null;
        }

        private static MethodInfo FindCompatibleSingleParameterMethod(Type type, string methodName, Type argumentType)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods = current.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1)
                        continue;

                    if (parameters[0].ParameterType.IsAssignableFrom(argumentType))
                        return method;
                }
            }

            return null;
        }

        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = null;
                try
                {
                    type = assemblies[i].GetType(fullName, false, false);
                }
                catch
                {
                    type = null;
                }

                if (type != null)
                    return type;
            }

            return null;
        }

        private static MethodInfo FindSingleDeclaredMethod(Type type, string methodName)
        {
            MethodInfo found = null;
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                if (found != null)
                    return null;

                found = method;
            }

            return found;
        }
    }
}
