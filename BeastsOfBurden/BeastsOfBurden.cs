using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace BeastsOfBurden
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    [BepInProcess("valheim.exe")]
    public class BeastsOfBurden : BaseUnityPlugin
    {
        const string pluginGUID = "com.mod.clevel";
        const string pluginName = "BeastsOfBurden";
        const string pluginVersion = "0.1.0";
        public static ManualLogSource logger;

        private readonly Harmony harmony = new Harmony(pluginGUID + "." + pluginName);

        static private ConfigEntry<bool> enableBeastsOfBurden;
        static private ConfigEntry<bool> commandWolf;
        static private ConfigEntry<bool> commandBoar;
        static private ConfigEntry<bool> commandLox;

        static private ConfigEntry<float> detachDistancePlayer;
        static private ConfigEntry<float> detachDistanceMediumAnimal;
        static private ConfigEntry<float> detachDistanceLox;

        static private ConfigEntry<Vector3> loxOffset;
        static private ConfigEntry<Vector3> smallerOffset;

        static private ConfigEntry<float> followDistanceLox;
        static private ConfigEntry<float> followDistanceMediumAnimal;

        void Awake()
        {
            logger = Logger;
            Logger.LogInfo($"Loading Beasts of Burden ");

            enableBeastsOfBurden = Config.Bind(pluginName,
                nameof(enableBeastsOfBurden),
                true,
                "Enable Beast of Burden Mod");

            commandWolf = Config.Bind(pluginName,
                nameof(commandWolf),
                true,
                "Makes Wolf Commandable (as it is in the normal game)");
            commandBoar = Config.Bind(pluginName,
                nameof(commandBoar),
                true,
                "Makes Boar Commandable");
            commandLox = Config.Bind(pluginName,
                 nameof(commandLox),
                 true,
                 "Makes Lox Commandable");

            detachDistancePlayer = Config.Bind(pluginName,
                nameof(detachDistancePlayer),
                2f, new ConfigDescription("How far the player has to be from the cart.",
                    new AcceptableValueRange<float>(1f, 5f)));

            detachDistancePlayer = Config.Bind(pluginName, nameof(detachDistancePlayer),
                2f, new ConfigDescription("How far the player has to be from the cart.",
                    new AcceptableValueRange<float>(1f, 5f)));

            detachDistanceMediumAnimal = Config.Bind(pluginName, nameof(detachDistanceMediumAnimal),
                3f, new ConfigDescription("How far the animal has to be from the cart.",
                    new AcceptableValueRange<float>(1f, 10f)));

            detachDistanceLox = Config.Bind(pluginName, nameof(detachDistanceLox),
                6f, new ConfigDescription("How far the Lox can be from the cart has to be from the cart.",
                new AcceptableValueRange<float>(1f, 10f)));

            loxOffset = Config.Bind(pluginName, nameof(loxOffset),
                new Vector3(0f, 0.8f, -2f), "Offset for where the cart attachs to the lox");

            smallerOffset = Config.Bind(pluginName, nameof(smallerOffset),
                new Vector3(0f, 0.8f, 0f), "Offset for smaller attachments (player, wolf, boar).");

            followDistanceLox = Config.Bind(pluginName, nameof(followDistanceLox),
                8f, new ConfigDescription("How close the lox should follow.",
                new AcceptableValueRange<float>(1f, 30f)));

            followDistanceMediumAnimal = Config.Bind(pluginName, nameof(followDistanceMediumAnimal),
                3f, new ConfigDescription("How close wolves or boars should follow.",
                new AcceptableValueRange<float>(1f, 30f)));

            harmony.PatchAll();
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        public enum Beasts
        {
            lox,
            wolf,
            boar,
            player,
            unknown
        }


        public static Beasts ParseCharacterType(Character c)
        {
            if (c.IsPlayer())
            {
                return Beasts.player;
            }
            else if (c.m_name.ToLower().Contains("lox"))
            {
                return Beasts.lox;
            }
            else if (c.m_name.ToLower().Contains("wolf"))
            {
                return Beasts.wolf;
            }
            else if (c.m_name.ToLower().Contains("boar"))
            {
                return Beasts.boar;
            }
            else
            {
                logger.LogError($"Unexpected character type {c}");
                return Beasts.unknown;
            }
        }



        public static Vector3 GetCartOffsetVectorForCharacter(Character c)
        {
            switch (ParseCharacterType(c))
            {
                case Beasts.lox:
                    return loxOffset.Value;
                case Beasts.wolf:
                    return smallerOffset.Value;
                case Beasts.boar:
                    return smallerOffset.Value;
                case Beasts.player:
                    return smallerOffset.Value;
                default:
                    logger.LogError($"Unexpected character type for {c.m_name}");
                    return Vector3.zero;
            }
        }


        public static float GetCartDetachDistance(Character c)
        {
            switch (ParseCharacterType(c))
            {
                case Beasts.lox:
                    return detachDistanceLox.Value;
                case Beasts.wolf:
                case Beasts.boar:
                    return detachDistanceMediumAnimal.Value;
                case Beasts.player:
                    return detachDistancePlayer.Value;
                default:
                    logger.LogError($"Unexpected character type for {c.m_name}");
                    return 0f;
            }
        }


        static Character FindClosestTamedAnimal(Vagon cart)
        {
            LogCartContents(cart);
            Transform attachPoint = cart.m_attachPoint;
            Character closest_animal = null;
            float closest_distance = float.MaxValue;

            foreach (Character currentCharacter in Character.GetAllCharacters())
            {
                if (currentCharacter != null && !currentCharacter.IsPlayer() && currentCharacter.IsTamed())
                {
                    Tameable tameable_component = currentCharacter.GetComponent<Tameable>();
                    Vector3 cartOffset = GetCartOffsetVectorForCharacter(currentCharacter);
                    Vector3 animalPosition = tameable_component.transform.position;

                    float distance = Vector3.Distance(tameable_component.transform.position + cartOffset, attachPoint.position);
                    float detachDistance = GetCartDetachDistance(currentCharacter);
                    if (distance < detachDistance && distance < closest_distance)
                    {
                        closest_animal = currentCharacter;
                        closest_distance = distance;
                    }
                }
            }
            if (closest_animal != null)
            {
                logger.LogDebug($"Closest animal is {closest_animal.m_name} at a distance of {closest_distance}");
            }
            return closest_animal;
        }

        static void LogCartContents(Vagon cart)
        {
            Container c = cart.m_container;
            logger.LogDebug($"Cart contents:");
            foreach (ItemDrop.ItemData item in c.GetInventory().GetAllItems())
            {
                logger.LogDebug($"\t * {item.m_shared.m_name}");
            }
        }

        /// <summary>
        /// This method is similar to Vagon.AttachTo except we don't call DetachAll as the first operation.
        /// </summary>
        /// <param name="attachTarget"></param>
        /// <param name="cart"></param>
        static void AttachCartTo(Character attachTarget, Vagon cart)
        {
            cart.m_attachOffset = GetCartOffsetVectorForCharacter(attachTarget);

            cart.m_attachJoin = cart.gameObject.AddComponent<ConfigurableJoint>();
            ((Joint)cart.m_attachJoin).autoConfigureConnectedAnchor = false;
            ((Joint)cart.m_attachJoin).anchor = cart.m_attachPoint.localPosition;
            ((Joint)cart.m_attachJoin).connectedAnchor = cart.m_attachOffset;
            ((Joint)cart.m_attachJoin).breakForce = cart.m_breakForce;
            cart.m_attachJoin.xMotion = ((ConfigurableJointMotion)1);
            cart.m_attachJoin.yMotion = ((ConfigurableJointMotion)1);
            cart.m_attachJoin.zMotion = ((ConfigurableJointMotion)1);
            SoftJointLimit linearLimit = default(SoftJointLimit);
            linearLimit.limit = 0.001f;
            cart.m_attachJoin.linearLimit = linearLimit;
            SoftJointLimitSpring linearLimitSpring = default(SoftJointLimitSpring);
            linearLimitSpring.spring = cart.m_spring;
            linearLimitSpring.damper = cart.m_springDamping;
            cart.m_attachJoin.linearLimitSpring = linearLimitSpring;
            cart.m_attachJoin.zMotion = ((ConfigurableJointMotion)0);
            cart.m_attachJoin.connectedBody = (attachTarget.gameObject.GetComponent<Rigidbody>());
        }


        [HarmonyPatch(typeof(Vagon), "LateUpdate")]
        class LateUpdate_Vagon_Patch
        {
            static void Prefix(ref Vagon __instance, ref ConfigurableJoint ___m_attachJoin, ref Rigidbody ___m_body)
            {
                if (___m_attachJoin != null && ___m_attachJoin.connectedBody == null)
                {
                    __instance.Detach();
                }
            }
        }


        [HarmonyPatch(typeof(Vagon), nameof(Vagon.InUse))]
        class InUse_Vagon_Patch
        {
            static bool Prefix(ref bool __result, ref Vagon __instance)
            {
                if ((bool)__instance.m_container && __instance.m_container.IsInUse())
                {
                    __result = true;
                }
                else if (__instance.IsAttached())
                {
                    __result = (bool)__instance.m_attachJoin.connectedBody.gameObject.GetComponent<Player>();
                }
                return false;
            }
        }


        [HarmonyPatch(typeof(Vagon), "FixedUpdate")]
        class Vagon_FixedUpdate_Patch
        {
            static bool Prefix(Vagon __instance)
            {
                // If mod is not enabled skip this function altogether
                if (!enableBeastsOfBurden.Value)
                {
                    return true;
                }

                if (!__instance.m_nview.IsValid())
                {
                    logger.LogDebug("m_nview invalid");
                    return false;
                }

                // Attempt to attach the cart
                __instance.UpdateAudio(Time.fixedDeltaTime);
                if (__instance.m_nview.IsOwner())
                {
                    if ((bool)__instance.m_useRequester)
                    {
                        if (__instance.IsAttached())
                        {
                            // If attached detach
                            __instance.Detach();
                        }
                        else
                        {
                            /// Determine if there is a valid animal in range and if so attempt to attach to it. 
                            /// If not attempt to attach to player
                            Character closest_tamed = FindClosestTamedAnimal(__instance);
                            if (closest_tamed != null)
                            {
                                AttachCartTo(closest_tamed, __instance);
                            }
                            else if (__instance.CanAttach(__instance.m_useRequester.gameObject))
                            {
                                AttachCartTo(__instance.m_useRequester, __instance);
                            }
                            else
                            {
                                __instance.m_useRequester.Message(MessageHud.MessageType.Center, "Not in the right position");
                            }
                        }
                        __instance.m_useRequester = null;
                    }
                    // This was a check in the other wagon code that I removed.
                    //if (__instance.IsAttached() && !__instance.CanAttach(((Component)(object)((Joint)__instance.m_attachJoin).connectedBody).gameObject))
                    //{
                    //    logger.LogDebug("Detach point 2");
                    //    __instance.Detach();
                    //}

                }
                else if (__instance.IsAttached())
                {
                    __instance.Detach();
                }
                return false;
            }
        }


        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.Follow))]
        class Tamed_Follow_patch
        {
            static bool Prefix(GameObject go, float dt, ref BaseAI __instance)
            {
                /// Allow normal follow code to run if character isn't tamed
                if (!__instance.m_character.IsTamed())
                {
                    return true;
                }

                // If the character isn't following a player allow the normal follow code to run
                if ((__instance as MonsterAI).GetFollowTarget().GetComponent<Player>() == null)
                {
                    return true;
                }

                float distance = Vector3.Distance(go.transform.position, __instance.transform.position);
                float followDistance;
                switch (ParseCharacterType(__instance.m_character))
                {
                    case Beasts.lox:
                        followDistance = followDistanceLox.Value;
                        break;
                    case Beasts.wolf:
                    case Beasts.boar:
                        followDistance = followDistanceMediumAnimal.Value;
                        break;
                    default:
                        logger.LogError($"Unexpected character type for {__instance.m_character.m_name}");
                        return true;
                }

                bool run = distance > followDistance * 3;
                if (distance < followDistance)
                {
                    __instance.StopMoving();
                }
                else
                {
                    __instance.MoveTo(dt, go.transform.position, 0f, run);
                }
                return false;
            }
        }


        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        class Command_Patch
        {
            /// <summary>
            /// Sets m_commandable to true for Tameable animals allowing commands to be issued to Boar, Wolf, and Lox
            /// </summary>
            /// <param name="___m_commandable"></param>
            static void Prefix(ref bool ___m_commandable, ref Character ___m_character)
            {
                logger.LogDebug($"Interacted with {___m_character.m_name}");
                switch (ParseCharacterType(___m_character))
                {
                    case Beasts.lox:
                        ___m_commandable = commandLox.Value;
                        return;
                    case Beasts.wolf:
                        ___m_commandable = commandWolf.Value;
                        return;
                    case Beasts.boar:
                        ___m_commandable = commandBoar.Value;
                        return;
                    default:
                        logger.LogError($"Unexpected character type for {___m_character.m_name}");
                        return;
                }
            }
        }
    }
}
