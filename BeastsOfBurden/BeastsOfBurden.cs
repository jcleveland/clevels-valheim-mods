using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;


namespace BeastsOfBurden
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class BeastsOfBurden : BaseUnityPlugin
    {
        const string pluginGUID = "org.bepinex.plugins.beasts_of_burden";
        const string pluginName = "BeastsOfBurden";
        public const string pluginVersion = "1.0.4";
        public static ManualLogSource logger;

        private readonly Harmony harmony = new Harmony(pluginGUID);

        static private ConfigEntry<bool> commandWolf;
        static private ConfigEntry<bool> commandBoar;
        static private ConfigEntry<bool> commandLox;

        static private ConfigEntry<bool> attachToWolf;
        static private ConfigEntry<bool> attachToBoar;
        static private ConfigEntry<bool> attachToLox;
        static private ConfigEntry<bool> attachToOtherTamed;

        static private ConfigEntry<float> detachDistanceFactor;
        static private ConfigEntry<float> detachDistancePlayer;

        static private ConfigEntry<float> followDistanceLox;
        static private ConfigEntry<float> followDistanceMediumAnimal;

        void Awake()
        {
            logger = Logger;
            Logger.LogInfo($"Loading Beasts of Burden ");

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

            attachToWolf = Config.Bind(pluginName,
                nameof(attachToWolf),
                true,
                "Allow cart to attach to Wolf");

            attachToBoar = Config.Bind(pluginName,
                nameof(attachToBoar),
                true,
                "Allow cart to attach to Boar");

            attachToLox = Config.Bind(pluginName,
                 nameof(attachToLox),
                 true,
                 "Allow cart to attach to Lox");

            attachToOtherTamed = Config.Bind(pluginName,
                 nameof(attachToOtherTamed),
                 true,
                 "Experimental: Allow cart to attach to other types of tamed animals. ");

            detachDistancePlayer = Config.Bind(pluginName, 
                nameof(detachDistancePlayer),
                2f, 
                new ConfigDescription("How far the player has to be from the cart.",
                    new AcceptableValueRange<float>(1f, 5f)));

            detachDistanceFactor = Config.Bind(pluginName,
                nameof(detachDistanceFactor),
                3.5f,
                new ConfigDescription("How far something has to be from the cart to use it a multiple of their radius",

                    new AcceptableValueRange<float>(1f, 5f)));
            followDistanceLox = Config.Bind(pluginName, nameof(followDistanceLox),
                8f, new ConfigDescription("How close the lox will follow behind the player.",
                new AcceptableValueRange<float>(1f, 30f)));

            followDistanceMediumAnimal = Config.Bind(pluginName, nameof(followDistanceMediumAnimal),
                3f, new ConfigDescription("How close medium animals (wolf and boar) will follow behind the player.",
                new AcceptableValueRange<float>(1f, 30f)));

            harmony.PatchAll();
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        /// <summary>
        /// An enum describing what is getting attached to the cart
        /// </summary>
        public enum Beasts
        {
            lox,
            wolf,
            boar,
            player,
            other
        }


        /// <summary>
        /// Parses a character into a Beasts
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
            public static Beasts ParseCharacterType(Character c)
        {
            if (c.IsPlayer())
            {
                return Beasts.player;
            }

            if (c.m_nview.IsValid())
            {
                switch (ZNetScene.instance.GetPrefab(c.m_nview.GetZDO().GetPrefab()).name)
                {
                    case "Lox":
                        return Beasts.lox;
                    case "Wolf":
                        return Beasts.wolf;
                    case "Boar":
                        return Beasts.boar;
                    default:
                        return Beasts.other;
                }
            }
            else
            {
                logger.LogDebug($"Character has invalid m_nview.");
                return Beasts.other;
            }
        }


        /// <summary>
        /// Different sized characters require different attachment offsets for the cart. 
        /// This will return the appropriate offset.
        /// </summary>
        /// <param name="c">to be attached to the cart</param>
        /// <returns>vector of where the cart should attach</returns>
        public static Vector3 GetCartOffsetVectorForCharacter(Character c)
        {
            if (c)
            {
                return new Vector3(0f, 0.8f, 0f - c.GetRadius());
            }
            return new Vector3(0f, 0.8f, 0f);
        }

        /// <summary>
        /// Allows the types of animals attached to to be configurable
        /// </summary>
        /// <param name="c"></param>
        /// <returns>if cart can be attached to character type</returns>
        public static bool IsAttachableCharacter(Character c)
        {
            switch (ParseCharacterType(c))
            {
                case Beasts.lox:
                    return attachToLox.Value;
                case Beasts.wolf:
                    return attachToWolf.Value;
                case Beasts.boar:
                    return attachToBoar.Value;
                case Beasts.player:
                    return true;
                default:
                    return attachToOtherTamed.Value;
            }
        }

        /// <summary>
        /// Different character types should be different distances to the cart for it to attach.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>the appropriate attach/detach distance for the provided character</returns>
        public static float GetCartDetachDistance(Character c)
        {
            if (c)
            {
                if (c.IsPlayer())
                {
                    return detachDistancePlayer.Value;
                }
                else
                {
                    return c.GetRadius() * detachDistanceFactor.Value;
                }
            
            }
            logger.LogError("Character pass was null");
            return 0f;
        }

        /// <summary>
        /// Searches nearby animals and finds the closest one to the cart that could be attached.
        /// </summary>
        /// <param name="cart"></param>
        /// <returns>Closest character to the cart that can attach to it, null if no character available</returns>
        static Character FindClosestAttachableAnimal(Vagon cart)
        {
            if (!cart)
            {
                logger.LogError("Cart pointer is null");
                return null;
            }

            Transform attachPoint = cart.m_attachPoint;
            Character closest_animal = null;
            float closest_distance = float.MaxValue;

            if (!cart.m_attachPoint)
            {
                logger.LogError("cart.m_attachPoint is null.");
                return null;
            }

            foreach (Character currentCharacter in Character.GetAllCharacters())
            {
                if(currentCharacter)
                {
                    if (!currentCharacter.IsPlayer() && currentCharacter.IsTamed() && IsAttachableCharacter(currentCharacter))
                    {
                        Vector3 cartOffset = GetCartOffsetVectorForCharacter(currentCharacter);
                        Vector3 animalPosition = currentCharacter.transform.position;

                        float distance = Vector3.Distance(animalPosition + cartOffset, attachPoint.position);
                        float detachDistance = GetCartDetachDistance(currentCharacter);
                        if (distance < detachDistance && distance < closest_distance)
                        {
                            closest_animal = currentCharacter;
                            closest_distance = distance;
                        }
                    }
                }
                else
                {
                    logger.LogWarning("null character returned by Character.GetAllCharacter() in method FindClosestTamedAnimal");
                }
            }
            if (closest_animal != null)
            {
                logger.LogDebug($"Closest animal is {closest_animal.m_name} at a distance of {closest_distance}");
            }
            return closest_animal;
        }


        /// <summary>
        /// Helper method to access the character currently attached to a cart
        /// </summary>
        /// <param name="cart"></param>
        /// <returns>Character currently attached</returns>
        static Character AttachedCharacter(Vagon cart)
        {
           if(cart && cart.IsAttached())
            {
                return cart.m_attachJoin.connectedBody.gameObject.GetComponent<Character>();
            }
            return null;
        }

        /// <summary>
        /// Logs the contents of a given cart to the debug logger. 
        /// Used during debugging to easily differentiate between carts.
        /// </summary>
        /// <param name="cart"></param>
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


        /// <summary>
        /// Patch for Vagon.LateUpdate that handles a situation where the attached animal is killed.
        /// </summary>
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

        /// <summary>
        /// Patch overriding InUse that will correctly return false if an animal is the one attached to a cart
        /// </summary>
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
                else
                {
                    __result = false;
                }
                return false;
            }
        }

        /// <summary>
        /// Patch to FixedUpdate that will attempt to attach cart to animal if there is an appropriate one nearby.
        /// </summary>
        [HarmonyPatch(typeof(Vagon), "FixedUpdate")]
        class Vagon_FixedUpdate_Patch
        {
            static bool Prefix(Vagon __instance)
            {
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
                            Character closest_tamed = FindClosestAttachableAnimal(__instance);
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
                    if (__instance.IsAttached()){
                        // Update detach distance before check if it should be detached
                        __instance.m_detachDistance = GetCartDetachDistance( AttachedCharacter(__instance) );
                        if (!__instance.CanAttach(((Component)(object)((Joint)__instance.m_attachJoin).connectedBody).gameObject))
                        {
                            __instance.Detach();
                            logger.LogDebug("Cart no longer attachable.");
                        }
                    }
                }
                else if (__instance.IsAttached())
                {
                    __instance.Detach();
                }
                return false;
            }
        }

        /// <summary>
        /// Patch for follow logic that allows for a greater follow distance.
        /// This is necessary because the lox tries to follow the player so closely that it constantly pushes the player
        /// Future use could include randomizing follow distance so multiple cart pulling animals are less likely to collide.
        /// </summary>
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
                        // Kick it back to the original method for unknown creature types
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

        /// <summary>
        /// Patch that allows this mod to specify which tamed animals are commandable.
        /// </summary>
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        class Command_Patch
        {
            /// <summary>
            /// Sets m_commandable to true for Tameable animals allowing commands to be issued to Boar, Wolf, and Lox
            /// </summary>
            /// <param name="___m_commandable"></param>
            static void Prefix(ref bool ___m_commandable, ref Character ___m_character)
            {
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
                        return;
                }
            }
        }
    }
}
