using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ImprovedHarpoon
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    [BepInProcess("valheim.exe")]
    public class ImprovedHarpoon : BaseUnityPlugin
    {
        const string pluginGUID = "org.mod.clevel";
        const string pluginName = "ImprovedHarpoon";
        const string pluginVersion = "0.1.0";
        public static ManualLogSource logger;

        private readonly Harmony harmony = new Harmony(pluginGUID + "." + pluginName);

        static private ConfigEntry<float> minForce;
        static private ConfigEntry<float> maxForceOnAttacker;
        static private ConfigEntry<float> maxForceOnHarpoon;
        static private ConfigEntry<float> maxMassRatioDiff;
        static private ConfigEntry<float> minHealthLimit;
        static private ConfigEntry<float> minDistance;
        static private ConfigEntry<float> maxDistance;
        static private ConfigEntry<float> scalingFactorForceOnAttacker;
        static private ConfigEntry<float> scalingFactorForceOnHarpoon;
        static private ConfigEntry<float> forceBaseline;

        static private ConfigEntry<float> pullStaminaCost;
        static private ConfigEntry<ForceMode> forceMode;

        static private ConfigEntry<float> onShipFactor;
        static private ConfigEntry<float> inWaterFactor;


        void Awake()
        {
            logger = Logger;
            logger.LogInfo($"Initializing {pluginName} {pluginVersion}");

            minForce = Config.Bind(
                pluginName,
                nameof(minForce),
                2f,
                "The minimum force a player will have applied to them if the harpooned character attempts to pull them.");

            maxForceOnAttacker = Config.Bind(
                pluginName,
                nameof(maxForceOnAttacker),
                50f,
                "The max force that will be applied to the harpoon thrower.");

            maxForceOnHarpoon = Config.Bind(
                pluginName,
                nameof(maxForceOnHarpoon),
                20f,
                "The max force that will be applied to the harpoon thrower.");

            minHealthLimit = Config.Bind(
                pluginName,
                nameof(minHealthLimit),
                0.33f,
                new ConfigDescription("The force an enemy applies to you scales with how much health they have. This value represents the percent of health they should stop applying any force at.",
                    new AcceptableValueRange<float>(0f, 1f)));

            minDistance = Config.Bind(
                pluginName,
                nameof(minDistance),
                5f,
                "If a character is this distance from the attacker they will not apply a force on them.");

            maxDistance = Config.Bind(
                pluginName,
                nameof(maxDistance),
                30f,
                "Max distance a character will attempt to pull the attacker. Used to smooth out applied force.");

            scalingFactorForceOnAttacker = Config.Bind(
                pluginName,
                nameof(scalingFactorForceOnAttacker),
                1.0f,
                "overall scaling factor to apply to end force applied to character");

            scalingFactorForceOnHarpoon = Config.Bind(
                pluginName,
                nameof(scalingFactorForceOnHarpoon),
                1.0f,
                "overall scaling factor to apply to end force applied to target");

            pullStaminaCost = Config.Bind(
                pluginName,
                nameof(pullStaminaCost),
                2f,
                "Stamina cost of blocking.");

            forceBaseline = Config.Bind(
                pluginName,
                nameof(forceBaseline),
                2f,
                "Baseline force applied.");

            maxMassRatioDiff = Config.Bind(
                pluginName,
                nameof(maxMassRatioDiff),
                0.9f,
                "Max difference in mass calculation for target and source");

            forceMode = Config.Bind(
                pluginName,
                nameof(forceMode),
                ForceMode.VelocityChange,
               "Force Mode to apply");

            onShipFactor = Config.Bind(
                pluginName,
                nameof(onShipFactor),
                1f,
                new ConfigDescription(
                    "Additional force applied if attacker is on a ship",
                    new AcceptableValueRange<float>(0f, 5f))
                );

            inWaterFactor = Config.Bind(
                pluginName,
                nameof(inWaterFactor),
                1f,
                new ConfigDescription(
                    "Additional force applied if harpooned is in water",
                    new AcceptableValueRange<float>(0f, 5f))
            );

            harmony.PatchAll();
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.UpdateStatusEffect))]
        class Base_StatusEffect_UpdateStatusEffect
        {
            [HarmonyReversePatch]
            public static void UpdateStatusEffect(StatusEffect instance, float dt)
            {
                instance.UpdateStatusEffect(dt);
            }
        }

        [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
        class SE_Harpooned_UpdateStatusEffect_Patch
        {
            static bool Prefix(float dt, ref SE_Harpooned __instance)
            {
                Base_StatusEffect_UpdateStatusEffect.UpdateStatusEffect(__instance, dt);

                if (!__instance.m_attacker || __instance.m_broken)
                {
                    return false;
                }

                Rigidbody harpooned_component = __instance.m_character.GetComponent<Rigidbody>();
                Rigidbody attacker_component = __instance.m_attacker.GetComponent<Rigidbody>();

                float relative_mass_harpooned = Mathf.Clamp(harpooned_component.mass / (attacker_component.mass + harpooned_component.mass), 1f - maxMassRatioDiff.Value, maxMassRatioDiff.Value);
                float relative_mass_attacker = 1.0f - relative_mass_harpooned;

                Vector3 position_diff = __instance.m_attacker.transform.position - __instance.m_character.transform.position;
                Vector3 direction = position_diff.normalized;
                float distance = position_diff.magnitude;

                // If blocking apply 
                if (__instance.m_attacker.IsBlocking() && distance > minDistance.Value)
                {
                    // If harpooning a player we ignore their health and consider them equal to the attacker
                    float health_force_multiplier;
                    if (__instance.m_character.IsPlayer())
                    {
                        health_force_multiplier = 0.5f;
                    } else
                    {
                        health_force_multiplier = (__instance.m_character.GetHealthPercentage() - minHealthLimit.Value) / (1.0f - minHealthLimit.Value);
                    }
                    
                    float force_on_harpoon = Mathf.Lerp(minForce.Value, maxForceOnHarpoon.Value, forceBaseline.Value * (1 - health_force_multiplier) * relative_mass_attacker * scalingFactorForceOnHarpoon.Value);
                    float force_on_attacker = Mathf.Lerp(minForce.Value, maxForceOnAttacker.Value, forceBaseline.Value * (health_force_multiplier) * relative_mass_harpooned * scalingFactorForceOnAttacker.Value);

                    Vector3 harpoon_force_vector = direction * force_on_harpoon;
                    Vector3 attacker_force_vector = -direction * force_on_attacker;
                    harpoon_force_vector.y = Mathf.Min(0, harpoon_force_vector.y);
                    attacker_force_vector.y = Mathf.Min(0, attacker_force_vector.y);

                    // If target is swimming then allow them to be pulled extra
                    if (__instance.m_character.IsSwiming())
                    {
                        logger.LogDebug($"Applying {nameof(inWaterFactor)}");
                        harpoon_force_vector *= inWaterFactor.Value;
                    }

                    if (__instance.m_attacker.GetStandingOnShip() != null)
                    {
                        // If attacker is on a ship allow them to be pulled more
                        logger.LogDebug($"Applying {nameof(onShipFactor)}");
                        attacker_force_vector *= onShipFactor.Value;

                        // If attacker and target are on a ship zero out the force vectors
                        // This resembles a patch IronWorks put in to handle buggy behavior
                        if (__instance.m_character.GetStandingOnShip() == __instance.m_attacker.GetStandingOnShip())
                        {
                            logger.LogDebug($"Applying zero forces - both players on same ship.");
                            harpoon_force_vector = Vector3.zero;
                            attacker_force_vector = Vector3.zero;
                        }
                    }

                    harpooned_component.AddForce(harpoon_force_vector, (ForceMode)forceMode.Value);
                    attacker_component.AddForce(attacker_force_vector, (ForceMode)forceMode.Value);

                    // Apply stamina drain
                    __instance.m_drainStaminaTimer += dt;
                    if (__instance.m_drainStaminaTimer > __instance.m_staminaDrainInterval)
                    {
                        float stamina_cost = pullStaminaCost.Value * (__instance.m_drainStaminaTimer / __instance.m_staminaDrainInterval);
                        __instance.m_attacker.UseStamina(stamina_cost);
                        __instance.m_drainStaminaTimer = 0f;
                    }

                    logger.LogDebug($"\n" +
                        $"Force on harpoon:  {harpoon_force_vector}  \t  mag: {harpoon_force_vector.magnitude}  \t mass: {harpooned_component.mass} \t rel_mass: {relative_mass_harpooned}\n" +
                        $"Force on attacker: {attacker_force_vector} \t mag: {attacker_force_vector.magnitude} \t mass: {attacker_component.mass}  \t rel_mass: {relative_mass_attacker} \n" +
                        $"Health factor: {health_force_multiplier} \n");
                }

                // Break connection if too far away or out of stamina
                if (distance > ImprovedHarpoon.maxDistance.Value)
                {
                    __instance.m_broken = true;
                    __instance.m_attacker.Message(MessageHud.MessageType.Center, "Line broke");
                }
                if (!__instance.m_attacker.HaveStamina() || __instance.m_attacker.IsStaggering())
                {
                    __instance.m_broken = true;
                    __instance.m_attacker.Message(MessageHud.MessageType.Center, __instance.m_character.m_name + " escaped");
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.IsDone))]
        class SE_Harpooned_IsDone_Patch
        {

            /// <summary>
            ///  Equivalent to SE_Harpooned except the attacker blocking does not end the harpooning, allows block to pull rope closer
            ///  also added condition where the attacker getting staggered will release the target
            /// </summary>
            /// <param name="__result"></param>
            /// <param name="__instance"></param>
            /// <returns></returns>
            static bool Prefix(ref bool __result, SE_Harpooned __instance)
            {

                /// base.IsDone() logic
                if (__instance.m_ttl > 0f && __instance.m_time > __instance.m_ttl)
                {
                    return true;
                }
                else if (__instance.m_broken)
                {
                    __result = true;
                }
                else if (!__instance.m_attacker)
                {
                    __result = true;
                }
                else if (__instance.m_time > 2f && __instance.m_attacker.InAttack())
                {
                    __instance.m_attacker.Message(MessageHud.MessageType.Center, __instance.m_character.m_name + " released");
                    __result = true;
                }
                else
                {
                    __result = false;
                }
                return false;
            }

        }
    }
}
