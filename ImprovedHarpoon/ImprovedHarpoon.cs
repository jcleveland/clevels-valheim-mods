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
        const string pluginGUID = "org.bepinex.plugins.improved_harpoon";
        const string pluginName = "ImprovedHarpoon";
        const string pluginVersion = "0.1.0";
        public static ManualLogSource logger;

        private readonly Harmony harmony = new Harmony(pluginGUID);

        static private ConfigEntry<float> minForce;
        static private ConfigEntry<float> maxForceOnAttacker;
        static private ConfigEntry<float> maxForceOnHarpoon;
        static private ConfigEntry<float> maxMassRatioDiff;
        static private ConfigEntry<float> maxMass;
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
                100f,
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
                0*2f,
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

            maxMass = Config.Bind(
                pluginName,
                nameof(maxMass),
                50f,
                "maxMass");

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

        public static void ApplyForces0(float dt, Character attacker, Character harpooned)
        {
            Rigidbody harpooned_component = harpooned.GetComponent<Rigidbody>();
            Rigidbody attacker_component = attacker.GetComponent<Rigidbody>();
            float relative_mass_harpooned = Mathf.Clamp(harpooned_component.mass / (attacker_component.mass + harpooned_component.mass), 1f - maxMassRatioDiff.Value, maxMassRatioDiff.Value);
            float relative_mass_attacker = 1.0f - relative_mass_harpooned;

            Vector3 position_diff = attacker.transform.position - harpooned.transform.position;
            Vector3 direction = position_diff.normalized;

            // If harpooning a player we ignore their health and consider them equal to the attacker
            float health_force_multiplier=0f;
            if (harpooned.IsPlayer())
            {
                health_force_multiplier = 0.5f;
            }
            else
            {
                health_force_multiplier = (harpooned.GetHealthPercentage() - minHealthLimit.Value) / (1.0f - minHealthLimit.Value);
            }

            ////////////
            ///
            Vector3 vector = attacker.transform.position - harpooned.transform.position;
            Vector3 normalized = vector.normalized;
            float radius = harpooned.GetRadius();
            float distance = vector.magnitude;
            float num = Mathf.Clamp01(Vector3.Dot(normalized, harpooned_component.velocity));
            float t = Utils.LerpStep(minDistance.Value, maxDistance.Value, distance);
            float num2 = Mathf.Lerp(minForce.Value, maxForceOnHarpoon.Value, t);
            float num3 = Mathf.Clamp01(maxMass.Value / harpooned_component.mass);
            float num4 = num2 * num3;

            if (!attacker.IsAttached())
            {
                Vector3 harpoon_force_vector = normalized * num4 * scalingFactorForceOnHarpoon.Value;
                Vector3 attacker_force_vector = -normalized * num4 * scalingFactorForceOnAttacker.Value;

                harpooned_component.AddForce(harpoon_force_vector, forceMode.Value);
                attacker_component.AddForce(attacker_force_vector, forceMode.Value);
                logger.LogDebug("\n"+
                $"Force on harpoon:  {harpoon_force_vector}  \t  mag: {harpoon_force_vector.magnitude}  \t mass: {harpooned_component.mass} \t rel_mass: {relative_mass_harpooned}\n" +
                $"Force on attacker: {attacker_force_vector} \t mag: {attacker_force_vector.magnitude} \t mass: {attacker_component.mass}  \t rel_mass: {relative_mass_attacker} \n" +
                $"Health factor: {health_force_multiplier} \n");

            }
        }



        public static void ApplyForces1(float dt, Character attacker, Character harpooned)
        {
            Rigidbody harpooned_component = harpooned.GetComponent<Rigidbody>();
            Rigidbody attacker_component = attacker.GetComponent<Rigidbody>();

            float relative_mass_harpooned = Mathf.Clamp(harpooned_component.mass / (attacker_component.mass + harpooned_component.mass), 1f - maxMassRatioDiff.Value, maxMassRatioDiff.Value);
            float relative_mass_attacker = 1.0f - relative_mass_harpooned;

            Vector3 position_diff = attacker.transform.position - harpooned.transform.position;
            Vector3 direction = position_diff.normalized;

            // If harpooning a player we ignore their health and consider them equal to the attacker
            float health_force_multiplier;
            if (harpooned.IsPlayer())
            {
                health_force_multiplier = 0.5f;
            }
            else
            {
                health_force_multiplier = (harpooned.GetHealthPercentage() - minHealthLimit.Value) / (1.0f - minHealthLimit.Value);
            }

            ////////////////////// I'm happy with the math above here for the most part


            float force_on_harpoon = Mathf.Lerp(minForce.Value, maxForceOnHarpoon.Value, forceBaseline.Value * (1 - health_force_multiplier) * relative_mass_attacker * scalingFactorForceOnHarpoon.Value);
            float force_on_attacker = Mathf.Lerp(minForce.Value, maxForceOnAttacker.Value, forceBaseline.Value * (health_force_multiplier) * relative_mass_harpooned * scalingFactorForceOnAttacker.Value);

            Vector3 harpoon_force_vector = direction * force_on_harpoon;
            Vector3 attacker_force_vector = -direction * force_on_attacker;
            harpoon_force_vector.y = Mathf.Min(0, harpoon_force_vector.y);
            attacker_force_vector.y = Mathf.Min(0, attacker_force_vector.y);


            
            ////////////// below here is aquatic based stuff
            // If target is swimming then allow them to be pulled extra
            if (harpooned.IsSwiming())
            {
                logger.LogDebug($"Applying {nameof(inWaterFactor)}");
                harpoon_force_vector *= inWaterFactor.Value;
            }

            if (attacker.GetStandingOnShip() != null)
            {
                // If attacker is on a ship allow them to be pulled more
                logger.LogDebug($"Applying {nameof(onShipFactor)}");
                attacker_force_vector *= onShipFactor.Value;

                // If attacker and target are on a ship zero out the force vectors
                // This resembles a patch IronWorks put in to handle buggy behavior
                if (harpooned.GetStandingOnShip() == attacker.GetStandingOnShip())
                {
                    logger.LogDebug($"Applying zero forces - both players on same ship.");
                    harpoon_force_vector = Vector3.zero;
                    attacker_force_vector = Vector3.zero;
                }
            }

            harpooned_component.AddForce(harpoon_force_vector, (ForceMode)forceMode.Value);
            attacker_component.AddForce(attacker_force_vector, (ForceMode)forceMode.Value);

            logger.LogDebug($"\n" +
                $"Force on harpoon:  {harpoon_force_vector}  \t  mag: {harpoon_force_vector.magnitude}  \t mass: {harpooned_component.mass} \t rel_mass: {relative_mass_harpooned}\n" +
                $"Force on attacker: {attacker_force_vector} \t mag: {attacker_force_vector.magnitude} \t mass: {attacker_component.mass}  \t rel_mass: {relative_mass_attacker} \n" +
                $"Health factor: {health_force_multiplier} \n");
        }



        public static void ApplyForces2(float dt, Character attacker, Character harpooned)
        {
            Rigidbody harpooned_component = harpooned.GetComponent<Rigidbody>();
            Rigidbody attacker_component = attacker.GetComponent<Rigidbody>();

            float relative_mass_harpooned = Mathf.Clamp(harpooned_component.mass / (attacker_component.mass + harpooned_component.mass), 1f - maxMassRatioDiff.Value, maxMassRatioDiff.Value);
            float relative_mass_attacker = 1.0f - relative_mass_harpooned;

            Vector3 position_diff = attacker.transform.position - harpooned.transform.position;
            Vector3 direction = position_diff.normalized;

            // If harpooning a player we ignore their health and consider them equal to the attacker
            float health_force_multiplier;
            if (harpooned.IsPlayer())
            {
                health_force_multiplier = 0.5f;
            }
            else
            {
                health_force_multiplier = (harpooned.GetHealthPercentage() - minHealthLimit.Value) / (1.0f - minHealthLimit.Value);
            }

            ////////////////////// I'm happy with the math above here for the most part


            float force_on_harpoon = Mathf.Lerp(minForce.Value, maxForceOnHarpoon.Value, forceBaseline.Value * (1 - health_force_multiplier) * relative_mass_attacker * scalingFactorForceOnHarpoon.Value);
            float force_on_attacker = Mathf.Lerp(minForce.Value, maxForceOnAttacker.Value, forceBaseline.Value * (health_force_multiplier) * relative_mass_harpooned * scalingFactorForceOnAttacker.Value);

            Vector3 harpoon_force_vector = direction * force_on_harpoon;
            Vector3 attacker_force_vector = -direction * force_on_attacker;
            harpoon_force_vector.y = Mathf.Min(0, harpoon_force_vector.y);
            attacker_force_vector.y = Mathf.Min(0, attacker_force_vector.y);

            if (harpooned.IsOnGround())
            {
            }

            ////////////// below here is aquatic based stuff
            // If target is swimming then allow them to be pulled extra
            if (harpooned.IsSwiming())
            {
                logger.LogDebug($"Applying {nameof(inWaterFactor)}");
                harpoon_force_vector *= inWaterFactor.Value;
            }

            if (attacker.GetStandingOnShip() != null)
            {
                // If attacker is on a ship allow them to be pulled more
                logger.LogDebug($"Applying {nameof(onShipFactor)}");
                attacker_force_vector *= onShipFactor.Value;

                // If attacker and target are on a ship zero out the force vectors
                // This resembles a patch IronWorks put in to handle buggy behavior
                if (harpooned.GetStandingOnShip() == attacker.GetStandingOnShip())
                {
                    logger.LogDebug($"Applying zero forces - both players on same ship.");
                    harpoon_force_vector = Vector3.zero;
                    attacker_force_vector = Vector3.zero;
                }
            }

            harpooned_component.AddForce(harpoon_force_vector, (ForceMode)forceMode.Value);
            attacker_component.AddForce(attacker_force_vector, (ForceMode)forceMode.Value);

            logger.LogDebug($"\n" +
                $"Force on harpoon:  {harpoon_force_vector}  \t  mag: {harpoon_force_vector.magnitude}  \t mass: {harpooned_component.mass} \t rel_mass: {relative_mass_harpooned}\n" +
                $"Force on attacker: {attacker_force_vector} \t mag: {attacker_force_vector.magnitude} \t mass: {attacker_component.mass}  \t rel_mass: {relative_mass_attacker} \n" +
                $"Health factor: {health_force_multiplier} \n");
        }



        public static void DrainStamina(float dt, SE_Harpooned harpoonEffect)
        {
            harpoonEffect.m_drainStaminaTimer += dt;
            if (harpoonEffect.m_drainStaminaTimer > harpoonEffect.m_staminaDrainInterval)
            {
                float stamina_cost = pullStaminaCost.Value * (harpoonEffect.m_drainStaminaTimer / harpoonEffect.m_staminaDrainInterval);
                harpoonEffect.m_attacker.UseStamina(stamina_cost);
                harpoonEffect.m_drainStaminaTimer = 0f;
            }
        }

        [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
        class SE_Harpooned_UpdateStatusEffect_Patch
        {
            static bool Prefix(float dt, ref SE_Harpooned __instance)
            {
                Base_StatusEffect_UpdateStatusEffect.UpdateStatusEffect(__instance, dt);
                Character attacker = __instance.m_attacker;
                Character harpooned = __instance.m_character;

                if (!attacker || __instance.m_broken)
                {
                    return false;
                }

                float distance = Vector3.Distance(attacker.transform.position, harpooned.transform.position);
                if (attacker.IsBlocking() && distance > minDistance.Value)
                {
                    ApplyForces0(dt, attacker, harpooned);
                    DrainStamina(dt, __instance);
                }

                // Break connection if too far away or out of stamina
                if (distance > ImprovedHarpoon.maxDistance.Value)
                {
                    __instance.m_broken = true;
                    attacker.Message(MessageHud.MessageType.Center, "Line broke");
                }
                if (!attacker.HaveStamina() || attacker.IsStaggering())
                {
                    __instance.m_broken = true;
                    attacker.Message(MessageHud.MessageType.Center, harpooned.m_name + " escaped");
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
