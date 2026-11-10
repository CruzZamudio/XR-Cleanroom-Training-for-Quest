// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEngine;

namespace PassthroughCameraSamples
{
    internal static class RequestPermissionsOnce
    {
        // Runs once after the first scene loads. Meta's sample skipped its start menu
        // and asked when the next scene loaded; this app has no start menu, so it asks right away.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            var ptLayerGo = new GameObject(nameof(OVRPassthroughLayer));
            Object.DontDestroyOnLoad(ptLayerGo);
            ptLayerGo.AddComponent<OVRPassthroughLayer>();

            OVRPermissionsRequester.Request(new[]
            {
                OVRPermissionsRequester.Permission.Scene,
                OVRPermissionsRequester.Permission.PassthroughCameraAccess
            });
        }
    }
}
