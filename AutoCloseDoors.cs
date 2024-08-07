using HarmonyLib;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main class for the Auto Close Doors Mod. Implements IModApi for mod integration.
/// </summary>
public class AutoCloseDoors : IModApi
{
    /// <summary>
    /// The delay in seconds before a door will automatically close.
    /// </summary>
    private static readonly float AutoCloseDelay = 5f;

    /// <summary>
    /// Dictionary to keep track of active auto-close coroutines.
    /// The key is the door position, and the value is the Coroutine handling the auto-close for that door.
    /// </summary>
    private static readonly Dictionary<Vector3i, Coroutine> ActiveAutoClosers = new Dictionary<Vector3i, Coroutine>();

    /// <summary>
    /// Initializes the mod. Called when the mod is loaded.
    /// </summary>
    /// <param name="_modInstance"></param>
    public void InitMod(Mod _modInstance)
    {
        try
        {
            Debug.Log("Auto Close Doors Mod: Initializing");
            var harmony = new Harmony("com.ephesius.AutoCloseDoors");
            harmony.PatchAll();
            Debug.Log("Auto Close Doors Mod: Harmony patches applied successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Auto-close Doors Mod: Failed to initialize." +
                           $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Contains the Harmony patch for the BlockDoor.OnBlockActivated method.
    /// </summary>
    [HarmonyPatch(typeof(BlockDoor))]
    [HarmonyPatch("OnBlockActivated")]
    [HarmonyPatch(new System.Type[] { typeof(WorldBase),
                                      typeof(int),
                                      typeof(Vector3i),
                                      typeof(BlockValue),
                                      typeof(EntityPlayerLocal) })]
    public class BlockDoorPatch
    {
        /// <summary>
        /// Prefix method that runs before the original OnBlockActivated method.
        /// It sets up the auto-close functionality when a door is opened.
        /// </summary>
        /// <param name="__instance">The BlockDoor instance.</param>
        /// <param name="_world">The world instance.</param>
        /// <param name="_cIdx">The chunk index.</param>
        /// <param name="_blockPos">The position of the door block.</param>
        /// <param name="_blockValue">The BlockValue of the door.</param>
        /// <param name="_player">The player interacting with the door.</param>
        /// <returns>True to allow the original method to run, false otherwise.</returns>
        static bool Prefix(BlockDoor __instance,
                           WorldBase _world,
                           int _cIdx,
                           Vector3i _blockPos,
                           BlockValue _blockValue,
                           EntityPlayerLocal _player)
        {
            try
            {
                bool isOpen = BlockDoor.IsDoorOpen(_blockValue.meta); // Check if door is currently open.
                if (!isOpen) // Door is being opened // If door is being opened (i.e. it's currently closed).
                {
                    // If there's already an auto-close coroutine for this door...
                    if (ActiveAutoClosers.TryGetValue(_blockPos, out Coroutine existingCoroutine))
                    {
                        // Then stop the existing coroutine and remove it from the tracking dictionary.
                        GameManager.Instance.StopCoroutine(existingCoroutine);
                        ActiveAutoClosers.Remove(_blockPos);
                    }

                    // Start a new auto-close coroutine.
                    Coroutine newCoroutine = GameManager.Instance.StartCoroutine(AutoCloseDoorCoroutine(__instance,
                                                                                                        _world,
                                                                                                        _cIdx,
                                                                                                        _blockPos,
                                                                                                        _blockValue));
                    // Add the new coroutine to the tracking dictionary.
                    ActiveAutoClosers[_blockPos] = newCoroutine;
                }

                return true; // Allow the original method to run.
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Auto-close Doors Mod: Unhandled error in OnBlockActivated patch." +
                               $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return true; // Run the original method if patch fails.
            }
        }

        /// <summary>
        /// Coroutine that handles the delayed auto-closing of a door.
        /// </summary>
        /// <param name="door">The BlockDoor instance</param>
        /// <param name="world">The world instance</param>
        /// <param name="cIdx">The chunk index</param>
        /// <param name="blockPos">The positin of the door block</param>
        /// <param name="blockValue">The BlockValue of the door</param>
        /// <returns>An IEnumerator for the coroutine</returns>
        static IEnumerator AutoCloseDoorCoroutine(BlockDoor door,
                                                  WorldBase world,
                                                  int cIdx,
                                                  Vector3i blockPos,
                                                  BlockValue blockValue)
        {
            yield return new WaitForSeconds(AutoCloseDelay); // Wait for the specified delay.

            try
            {
                CloseDoorAfterDelay(door, world, cIdx, blockPos, blockValue); // Attempt to close the door.
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Auto-close Doors Mod: Error in AutoCloseDoorCoroutine." +
                               $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");
            }
            finally
            {
                ActiveAutoClosers.Remove(blockPos); // Remove this coroutine from tracking dict, success or failure.
            }
        }

        /// <summary>
        /// Closes the door after specified delay if it's still open.
        /// </summary>
        /// <param name="door">The BlockDoor instance.</param>
        /// <param name="world">The world instance.</param>
        /// <param name="cIdx">The chunk index.</param>
        /// <param name="blockPos">The position of the door block.</param>
        /// <param name="blockValue">The BlockValue of the door.</param>
        static void CloseDoorAfterDelay(BlockDoor door,
                                        WorldBase world,
                                        int cIdx,
                                        Vector3i blockPos,
                                        BlockValue blockValue)
        {
            BlockValue currentBlockValue = world.GetBlock(blockPos); // Get the current state of the door.
            if (BlockDoor.IsDoorOpen(currentBlockValue.meta)) // If the door is still open...
            {
                // Then close the door and play the door closing sound.
                door.OnBlockActivated("close", world, cIdx, blockPos, currentBlockValue, null);
                door.HandleOpenCloseSound(false, blockPos);
            }
        }
    }
}