using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SubmersedVRTweaks
{
    // this was from when I was using vrHands as my forward reference (instead of the raw controller)
    // so I used this so it would make sure to load my mod after the vrHands had been loaded
    // because they load when awake is called, and start is called after awake
    // At some point I should change this to use just the game loading or something
    // bc vrCameraRig is created at the main menu, not in game
    [HarmonyPatch(typeof(ArmsController))]
    public class UnderwaterMotorPatchFunctions
    {
        private const string VRCameraRigTypeName = "SubmersedVR.VRCameraRig, SubmersedVR";
        private static FieldInfo _rightControllerField;
        // Caches so that we don't have to search for them every time UpdateMove() is called
        private static UnityEngine.Object _vrCameraRig;
        private static Player _playerCache;
        private static bool _not_working = false;
        
        [HarmonyPatch(nameof(ArmsController.Start))]
        [HarmonyPostfix]
        static void CacheVRComponents()
        {
            TryCacheVRComponents(logOnFailure: false);
        }

        private static void TryCacheVRComponents(bool logOnFailure = true)
        {
            // Need this because VRCameraRig is an internal class
            Type typeVRCameraRig = Type.GetType(VRCameraRigTypeName);
            if (typeVRCameraRig == null)
            {
                if (logOnFailure)
                {
                    SubmersedVRTweaksPlugin.Log.LogError("VRCameraRig type could not be found. Is SubmersedVR installed?");
                }
                return;
            }

            _rightControllerField ??= typeVRCameraRig.GetField("rightController",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            if (_vrCameraRig == null)
            {
                _vrCameraRig = GameObject.FindObjectOfType(typeVRCameraRig);
            }

            if (_playerCache == null)
            {
                _playerCache = GameObject.FindObjectOfType<Player>();
            }
        }

        private static Player GetPlayer()
        {
            if (_playerCache == null)
            {
                _playerCache = GameObject.FindObjectOfType<Player>();
            }

            return _playerCache;
        }

        private static bool ShouldUseHandSteering()
        {
            bool steerUnderWater = SubmersedVRTweaksPlugin.ModOptions?.SteerWithHandUnderWater == true;
            bool steerSeaglide = SubmersedVRTweaksPlugin.ModOptions?.SteerWithHandForSeaglide == true;

            if (!steerUnderWater && !steerSeaglide)
            {
                return false;
            }

            if (steerUnderWater)
            {
                return true;
            }

            Player player = GetPlayer();
            return player != null && player.motorMode == Player.MotorMode.Seaglide;
        }

        private static bool TryGetRightController(out GameObject rightController)
        {
            rightController = null;

            if (_not_working)
            {
                return false;
            }

            if (_vrCameraRig == null || _rightControllerField == null)
            {
                TryCacheVRComponents();
            }

            if (_vrCameraRig == null || _rightControllerField == null)
            {
                SubmersedVRTweaksPlugin.Log.LogError("VRCameraRig/rightController field not found. Have you installed SubmersedVR?");
                _not_working = true;
                return false;
            }

            try
            {
                rightController = _rightControllerField.GetValue(_vrCameraRig) as GameObject;
            }
            catch (TargetException ex)
            {
                SubmersedVRTweaksPlugin.Log.LogError($"Failed to read rightController from VRCameraRig: {ex.Message}");
                _not_working = true;
                return false;
            }

            if (rightController == null)
            {
                SubmersedVRTweaksPlugin.Log.LogError("Right Controller not found! Have you installed SubmersedVR?");
                _not_working = true;
                return false;
            }

            return true;
        }
        
        public static Vector3 get_rotation(Quaternion playerForward, Vector3 inputDirectionNoY)
        {
            // Default implementation, if submersed isn't installed the game won't just break
            if (_not_working || !ShouldUseHandSteering())
            {
                return playerForward * inputDirectionNoY;
            }

            if (!TryGetRightController(out GameObject rightController))
            {
                return playerForward * inputDirectionNoY;
            }

            // fix rotation, by default when pressing forward you go up 45 degrees as well
            Quaternion rotationOffset;
            Player player = GetPlayer();

            if (player != null && player.motorMode == Player.MotorMode.Seaglide && SubmersedVRTweaksPlugin.ModOptions?.SteerWithHandForSeaglide == true)
            {
                // seaglide is weird as well, this seems to work
                rotationOffset = Quaternion.Euler(60f, 0f, 0f);
            }
            else
            {
                // rotate 45 degrees down so when going straight forwards it is just straight forwards
                rotationOffset = Quaternion.Euler(45f, 0f, 0f);
            }

            return rightController.transform.rotation * rotationOffset * inputDirectionNoY;
        }
    }

    // Underwater motor controls underwater movement (lol)
    [HarmonyPatch(typeof(UnderwaterMotor))]
    [HarmonyPatch(nameof(UnderwaterMotor.UpdateMove))]
    public class UnderwaterMotorUpdateMovePatch
    {
        // make it more reflection i think, i just stole this from the example
        // could also be making it IL compatible, idk
        private static readonly MethodInfo GetRotationMethod = AccessTools.Method(typeof(UnderwaterMotorPatchFunctions), nameof(UnderwaterMotorPatchFunctions.get_rotation));
        
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                // Most stupid match cos I don't know how to do operands lol
                // c# version: Vector3 vector3_2 = this.playerController.forwardReference.rotation * vector3_1;
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Ldloc_2),
                    new CodeMatch(OpCodes.Call), // <--- this is the multiply bit
                    new CodeMatch(OpCodes.Stloc_S)
                    )
                .Advance(5) // move to the multiply call
                // replace the func call with my custom function, args are the same so no loading vars or anything needed :)
                .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Call, GetRotationMethod))
                // return the modified instructions
                .InstructionEnumeration();
        }
    }
}
