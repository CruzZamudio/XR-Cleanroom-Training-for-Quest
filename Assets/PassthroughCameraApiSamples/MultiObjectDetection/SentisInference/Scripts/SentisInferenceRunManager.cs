// Based on Meta's PassthroughCameraApiSamples (c) Meta Platforms, Inc. and affiliates.
// Modified: supports switching between multiple detection models at runtime
// (e.g. stock COCO YOLOv9 and a custom-trained YOLO) via DetectionModelProfile assets.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Meta.XR;
using QuestApp.Detection;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    public class SentisInferenceRunManager : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private DetectionUiMenuManager m_uiMenuManager;
        [SerializeField] private DetectionManager m_detectionManager;

        [Header("Detection models")]
        [Tooltip("Every model the user can pick from. Index 0 is the default.")]
        [SerializeField] private List<DetectionModelProfile> m_modelProfiles = new();
        [SerializeField] private int m_defaultProfileIndex = 0;
        [Tooltip("Reopen the app on whichever model the user picked last.")]
        [SerializeField] private bool m_rememberLastChoice = true;

        [Header("UI display references")]
        [SerializeField] private SentisInferenceUiManager m_uiInference;

        [Header("[Editor Only] Convert ONNX to Sentis")]
        [Tooltip("Drop a YOLOv8/v9/v11-style ONNX here, then press the convert button below.")]
        public ModelAsset OnnxModel;
        [Tooltip("File name for the converted model. It is saved next to the ONNX file.")]
        public string ConvertedModelName = "custom_yolo";
        [Space(40)]

        private const string k_prefsKey = "QuestApp.ActiveDetectionModel";

        private Worker m_engine;
        private Vector2Int m_inputSize;
        private int m_labelCount;
        private int m_pendingProfileIndex = -1;
        private bool m_warnedLabelMismatch;
        private readonly List<(int classId, Vector4 boundingBox)> m_detections = new();

        /// <summary>Fired just before a model switch starts (use it to show a loading state).</summary>
        public event Action<DetectionModelProfile> ModelSwitchStarted;
        /// <summary>Fired once the new model is loaded and running.</summary>
        public event Action<DetectionModelProfile> ModelChanged;

        public IReadOnlyList<DetectionModelProfile> ModelProfiles => m_modelProfiles;
        public DetectionModelProfile ActiveProfile { get; private set; }
        public int ActiveProfileIndex { get; private set; } = -1;
        public bool IsSwitchingModel { get; private set; }

        #region Unity lifecycle

        private void Awake()
        {
            if (m_modelProfiles.Count == 0)
            {
                Debug.LogError("[Detection] No model profiles assigned to SentisInferenceRunManager.");
                enabled = false;
                return;
            }

            var index = m_defaultProfileIndex;
            if (m_rememberLastChoice)
            {
                index = PlayerPrefs.GetInt(k_prefsKey, index);
            }
            index = Mathf.Clamp(index, 0, m_modelProfiles.Count - 1);

            if (!TryLoadProfile(index) && index != m_defaultProfileIndex)
            {
                TryLoadProfile(Mathf.Clamp(m_defaultProfileIndex, 0, m_modelProfiles.Count - 1));
            }
        }

        private IEnumerator Start()
        {
            // Loaded in Awake, but announce it once everyone has subscribed.
            if (ActiveProfile != null) ModelChanged?.Invoke(ActiveProfile);

            while (true)
            {
                // Model switches are applied only between inference runs, never while
                // RunInference is still waiting on the old worker's outputs.
                if (m_pendingProfileIndex >= 0)
                {
                    yield return SwitchModelRoutine();
                    continue;
                }

                if (m_engine == null || m_uiMenuManager.IsPaused)
                {
                    yield return null;
                    continue;
                }

                yield return RunInference();
            }
        }

        private void OnDestroy() => DisposeEngine();

        #endregion

        #region Model switching (public API)

        /// <summary>Request a switch to the model at <paramref name="index"/>. Applied on the next frame between inferences.</summary>
        public void SelectModel(int index)
        {
            if (index < 0 || index >= m_modelProfiles.Count)
            {
                Debug.LogWarning($"[Detection] Model index {index} is out of range.");
                return;
            }
            if (index == ActiveProfileIndex && !IsSwitchingModel) return;
            m_pendingProfileIndex = index;
        }

        public void SelectModel(DetectionModelProfile profile) => SelectModel(m_modelProfiles.IndexOf(profile));

        /// <summary>Cycle to the next model in the list (handy for a controller button).</summary>
        public void SelectNextModel() => SelectModel((ActiveProfileIndex + 1) % m_modelProfiles.Count);

        private IEnumerator SwitchModelRoutine()
        {
            var index = m_pendingProfileIndex;
            m_pendingProfileIndex = -1;

            IsSwitchingModel = true;
            ModelSwitchStarted?.Invoke(m_modelProfiles[index]);
            yield return null; // let the loading state render before the (blocking) load + warm-up

            var previous = ActiveProfileIndex;
            DisposeEngine();
            m_uiInference.ClearAnnotations();

            if (!TryLoadProfile(index) && previous >= 0)
            {
                Debug.LogWarning("[Detection] Falling back to the previous model.");
                TryLoadProfile(previous);
            }

            IsSwitchingModel = false;
            if (ActiveProfile != null) ModelChanged?.Invoke(ActiveProfile);
        }

        private bool TryLoadProfile(int index)
        {
            var profile = m_modelProfiles[index];
            if (profile == null)
            {
                Debug.LogError($"[Detection] Can't load model {index}: slot is empty.");
                return false;
            }
            if (!profile.IsValid(out var problem))
            {
                Debug.LogError($"[Detection] Can't load model {index}: {problem}");
                return false;
            }

            Model model;
            try
            {
                model = ModelLoader.Load(profile.SentisModel);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Detection] Failed to load '{profile.DisplayName}': {e.Message}");
                return false;
            }

            if (model.outputs.Count < 3)
            {
                Debug.LogError($"[Detection] '{profile.DisplayName}' has {model.outputs.Count} output(s), expected 3 (boxes, class IDs, scores). " +
                               "Assign the converted .sentis file, not the raw .onnx.");
                return false;
            }

            var inputShape = model.inputs[0].shape;
            m_inputSize = new Vector2Int(inputShape.Get(2), inputShape.Get(3));
            m_engine = new Worker(model, profile.Backend);
            m_labelCount = profile.GetLabelLines().Length;
            m_warnedLabelMismatch = false;
            m_uiInference.SetLabels(profile.Labels);

            WarmUp();

            ActiveProfile = profile;
            ActiveProfileIndex = index;
            if (m_rememberLastChoice)
            {
                PlayerPrefs.SetInt(k_prefsKey, index);
                PlayerPrefs.Save();
            }
            Debug.Log($"[Detection] Active model: {profile.DisplayName} ({m_labelCount} classes, input {m_inputSize.x}x{m_inputSize.y}, {profile.Backend})");
            return true;
        }

        /// <summary>The first inference blocks for a long time, so do it now (while the loading state shows) rather than mid-session.</summary>
        private void WarmUp()
        {
            using var input = new Tensor<float>(new TensorShape(1, 3, m_inputSize.x, m_inputSize.y));
            m_engine.Schedule(input);
            CompletePendingOutputs();
        }

        /// <summary>Kept for the sample's StartMenu, which pre-warms the default model at app launch.</summary>
        internal static void PreloadModel(ModelAsset modelAsset)
        {
            if (modelAsset == null) return;
            var model = ModelLoader.Load(modelAsset);
            var inputShape = model.inputs[0].shape;
            using var worker = new Worker(model, BackendType.CPU);
            using var input = new Tensor<float>(new TensorShape(1, 3, inputShape.Get(2), inputShape.Get(3)));
            worker.Schedule(input);
            for (var i = 0; i < model.outputs.Count; i++)
            {
                worker.PeekOutput(i)?.CompleteAllPendingOperations();
            }
        }

        private void CompletePendingOutputs()
        {
            if (m_engine == null) return;
            for (var i = 0; i < 3; i++)
            {
                m_engine.PeekOutput(i)?.CompleteAllPendingOperations();
            }
        }

        private void DisposeEngine()
        {
            if (m_engine == null) return;
            CompletePendingOutputs();
            m_engine.Dispose();
            m_engine = null;
        }

        #endregion

        #region Inference (from Meta's sample, thresholds now per-model)

        private IEnumerator RunInference()
        {
            if (!m_cameraAccess.IsPlaying)
            {
                yield break;
            }

            [DllImport("OVRPlugin", CallingConvention = CallingConvention.Cdecl)]
            static extern OVRPlugin.Result ovrp_GetNodePoseStateAtTime(double time, OVRPlugin.Node nodeId, out OVRPlugin.PoseStatef nodePoseState);
            if (!ovrp_GetNodePoseStateAtTime(OVRPlugin.GetTimeInSeconds(), OVRPlugin.Node.Head, out _).IsSuccess())
            {
                yield break;
            }

            var cachedCameraPose = m_cameraAccess.GetCameraPose();
            Texture targetTexture = m_cameraAccess.GetTexture();

            var textureTransform = new TextureTransform().SetDimensions(targetTexture.width, targetTexture.height, 3);
            using var input = new Tensor<float>(new TensorShape(1, 3, m_inputSize.x, m_inputSize.y));
            TextureConverter.ToTensor(targetTexture, input, textureTransform);

            m_engine.Schedule(input);

            var boxesAwaiter = (m_engine.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
            while (!boxesAwaiter.IsCompleted) yield return null;
            using var boxes = boxesAwaiter.GetResult();
            if (boxes.shape[0] == 0) yield break;

            var classIDsAwaiter = (m_engine.PeekOutput(1) as Tensor<int>).ReadbackAndCloneAsync().GetAwaiter();
            while (!classIDsAwaiter.IsCompleted) yield return null;
            using var classIDs = classIDsAwaiter.GetResult();
            if (classIDs.shape[0] == 0) yield break;

            var scoresAwaiter = (m_engine.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
            while (!scoresAwaiter.IsCompleted) yield return null;
            using var scores = scoresAwaiter.GetResult();
            if (scores.shape[0] == 0) yield break;

            NonMaxSuppression(m_detections, boxes, classIDs, scores, ActiveProfile.IouThreshold, ActiveProfile.ScoreThreshold);
            DropUnlabeledDetections();

            if (!m_cameraAccess.IsPlaying || m_detectionManager.m_spatialAnchor == null || !m_detectionManager.m_spatialAnchor.IsTracked)
            {
                yield break;
            }

            m_uiInference.DrawUIBoxes(m_detections, m_inputSize, cachedCameraPose);
        }

        /// <summary>Guards against a labels file with fewer lines than the model has classes (would otherwise throw in the UI).</summary>
        private void DropUnlabeledDetections()
        {
            var removed = m_detections.RemoveAll(d => d.classId < 0 || d.classId >= m_labelCount);
            if (removed > 0 && !m_warnedLabelMismatch)
            {
                m_warnedLabelMismatch = true;
                Debug.LogWarning($"[Detection] '{ActiveProfile.DisplayName}' produced class IDs beyond its {m_labelCount} labels. " +
                                 "Check the labels file matches the model's class order.");
            }
        }

        private static void NonMaxSuppression(List<(int classId, Vector4 boundingBox)> outDetections, Tensor<float> boxes, Tensor<int> classIDs, Tensor<float> scores, float iouThreshold, float scoreThreshold)
        {
            outDetections.Clear();

            var filteredIndices = new List<int>();
            NativeArray<float>.ReadOnly scoresArray = scores.AsReadOnlyNativeArray();
            for (var i = 0; i < scoresArray.Length; i++)
            {
                if (scoresArray[i] >= scoreThreshold) filteredIndices.Add(i);
            }
            if (filteredIndices.Count == 0) return;

            filteredIndices.Sort((a, b) => scoresArray[b].CompareTo(scoresArray[a]));

            var suppressed = new bool[filteredIndices.Count];
            for (var i = 0; i < filteredIndices.Count; i++)
            {
                if (suppressed[i]) continue;
                var idx = filteredIndices[i];
                outDetections.Add((classIDs[idx], GetBox(idx)));

                for (var j = i + 1; j < filteredIndices.Count; j++)
                {
                    if (suppressed[j]) continue;
                    if (CalculateIoU(GetBox(idx), GetBox(filteredIndices[j])) > iouThreshold) suppressed[j] = true;
                }
            }

            Vector4 GetBox(int i) => new Vector4(boxes[i, 0], boxes[i, 1], boxes[i, 2], boxes[i, 3]);
        }

        internal static float CalculateIoU(Vector4 boxA, Vector4 boxB)
        {
            var x1 = Mathf.Max(boxA.x, boxB.x);
            var y1 = Mathf.Max(boxA.y, boxB.y);
            var x2 = Mathf.Min(boxA.z, boxB.z);
            var y2 = Mathf.Min(boxA.w, boxB.w);

            var intersectionArea = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
            var boxAArea = (boxA.z - boxA.x) * (boxA.w - boxA.y);
            var boxBArea = (boxB.z - boxB.x) * (boxB.w - boxB.y);
            var unionArea = boxAArea + boxBArea - intersectionArea;

            return unionArea == 0 ? 0 : intersectionArea / unionArea;
        }

        #endregion
    }
}
