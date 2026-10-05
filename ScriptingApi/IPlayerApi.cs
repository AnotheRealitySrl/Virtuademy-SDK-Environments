using System;

using UnityEngine;

using Virtuademy.ScriptingApi;

using System.Collections;

namespace Virtuademy.Environments.ScriptingApi
{
    /// <summary>
    /// The local player: where they are, what they can do, what is visible, and where the camera
    /// looks from.
    /// </summary>
    public interface IPlayerApi
    {
        /// <summary>
        /// The character's transform. Null before the avatar exists — early in a scene, or in a
        /// world the player has not been embodied into yet.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy CMUser: Get Character Transform</c>.</remarks>
        Transform Root { get; }

        /// <summary>The head transform, for anything that has to follow the player's gaze.</summary>
        /// <remarks>Node: <c>Virtuademy CMUser: Get Character Head Transform</c>.</remarks>
        Transform Head { get; }

        /// <summary>The left interactor transform. Null on platforms with no hands.</summary>
        /// <remarks>Node: <c>Virtuademy CMUser: Get Character Left Hand</c>.</remarks>
        Transform LeftHand { get; }

        /// <summary>The right interactor transform. Null on platforms with no hands.</summary>
        /// <remarks>Node: <c>Virtuademy CMUser: Get Character Right Hand</c>.</remarks>
        Transform RightHand { get; }

        /// <summary>
        /// Moves the player to <paramref name="destination"/>, fading to black and back so the cut
        /// is not jarring. <paramref name="onArrived"/> runs once the fade has finished.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Character: Teleport</c>.</remarks>
        IEnumerator Teleport(Transform destination, Action onArrived = null);

        /// <summary>
        /// Moves the player to an explicit pose, with the same fade. For destinations a script
        /// computes rather than reads off a scene object.
        /// </summary>
        IEnumerator Teleport(Vector3 position, Quaternion rotation, Action onArrived = null);

        /// <summary>
        /// Enables or disables the player's own movement input, leaving every other input setting
        /// alone. Use it for a cutscene the player watches from where they stand.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Character: Enable Movement</c>.</remarks>
        void EnableMovement(bool enable);

        /// <summary>
        /// Shows or hides the local player's own avatar meshes. Half-body avatars show hands only,
        /// so this is what gets the player's own body out of a close-up shot.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Character: Enable Mesh</c>.</remarks>
        void ShowOwnAvatar(bool visible);

        /// <summary>
        /// Shows or hides everybody else's avatars. Affects rendering only — the other players are
        /// still there and still hear the room.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Scene: Enable Other Players</c>.</remarks>
        void ShowOtherAvatars(bool visible);

        #region The camera

        /// <summary>Switches to the first-person camera.</summary>
        /// <remarks>Node: <c>Virtuademy Character: Set First Person Camera Mode</c>.</remarks>
        void SetFirstPersonCamera();

        /// <summary>Switches to the third-person camera.</summary>
        /// <remarks>Node: <c>Virtuademy Character: Set Third Person Camera Mode</c>.</remarks>
        void SetThirdPersonCamera();

        /// <summary>
        /// Leaves the camera fixed and takes every input away from the player: no movement, no
        /// dragging, no zoom.
        /// </summary>
        /// <remarks>
        /// These three arrangements are the ones the platform supports, named for what they do. The
        /// nodes behind them used to build a settings object out of five booleans; that object
        /// belongs to the application and does not cross this boundary.
        /// <para>Node: <c>Virtuademy Camera: Set camera mode</c> with a static camera.</para>
        /// </remarks>
        void UseStaticCamera(bool constrainRotation = false);

        /// <summary>
        /// Lets the player rotate the camera by dragging, and nothing else — no movement, no zoom.
        /// </summary>
        void UseDragRotationCamera(bool constrainRotation = false);

        /// <summary>
        /// Gives the camera back its full range — dragging, third person and zoom — while the player
        /// still cannot move.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Camera: Set camera mode</c> with a free camera.</remarks>
        void UseFreeCamera(bool constrainRotation = false);

        /// <summary>Sets the camera's rotation speed on both axes.</summary>
        /// <remarks>Node: <c>Virtuademy Camera: ChangeCameraSpeed</c>.</remarks>
        void SetCameraSpeed(float xSpeed, float ySpeed);

        /// <summary>
        /// Moves the camera to a point and leaves it there. <paramref name="onArrived"/> runs when
        /// it has stopped.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Character: Move camera to point</c>.</remarks>
        IEnumerator MoveCameraTo(Transform target, Action onArrived = null);

        /// <summary>
        /// Enters the pan state around <paramref name="target"/> — the player looks at a thing and
        /// can orbit it. <see cref="ExitCameraPan"/> gives movement back.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Character: Pan</c>.</remarks>
        IEnumerator PanCameraAround(Transform target, Action onReady = null);

        /// <summary>
        /// The same, with the bounds the free-camera node exposes as ports rather than the
        /// character controller's defaults.
        /// </summary>
        /// <remarks>Node: <c>Virtuademy Character: Free Pan</c>.</remarks>
        IEnumerator PanCameraAround(Transform target,
                             float maxZoom,
                             float minZoom,
                             float maxYRotation,
                             float minYRotation,
                             float maxXRotation,
                             float minXRotation,
                             bool cameraInteraction,
                             Action onReady = null);

        /// <summary>Leaves the pan state and hands movement back to the player.</summary>
        /// <remarks>Node: <c>Virtuademy Character: Exit Pan</c>.</remarks>
        IEnumerator ExitCameraPan(Action onDone = null);

        #endregion
    
        /// <summary>
        /// Puts camera and movement back the way the environment was authored, undoing whatever a
        /// graph did to them. The placeholder that owns the environment's input settings calls this
        /// on teardown, so a world does not leak a locked camera into the next one.
        /// </summary>
        /// <remarks>Node: <c>Set Default Settings</c>.</remarks>
        void ApplyDefaultInputSettings();
}
}
