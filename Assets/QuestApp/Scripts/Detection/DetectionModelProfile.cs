using Unity.InferenceEngine;
using UnityEngine;

namespace QuestApp.Detection
{
    /// <summary>
    /// One selectable detection model: the converted .sentis file, its class labels, and its tuning.
    /// Create via Assets > Create > Quest App > Detection Model Profile.
    /// Make one for the stock COCO model and one for each custom YOLO model.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDetectionModel", menuName = "Quest App/Detection Model Profile")]
    public class DetectionModelProfile : ScriptableObject
    {
        [Tooltip("Name shown in the in-headset model picker.")]
        public string DisplayName = "New Model";

        [TextArea(2, 4)]
        [Tooltip("Optional one-line description shown under the name.")]
        public string Description;

        [Tooltip("The .sentis model produced by the converter (it has the NMS outputs baked in). Not the raw .onnx.")]
        public ModelAsset SentisModel;

        [Tooltip("Text file with one class name per line, in the same order the model was trained with.")]
        public TextAsset Labels;

        [Tooltip("CPU matches Meta's sample. GPUCompute can be faster for larger models; test both on device.")]
        public BackendType Backend = BackendType.CPU;

        [Range(0, 1)] public float IouThreshold = 0.6f;
        [Range(0, 1)] public float ScoreThreshold = 0.23f;

        public bool IsValid(out string problem)
        {
            if (SentisModel == null) { problem = $"'{name}' has no Sentis model assigned."; return false; }
            if (Labels == null) { problem = $"'{name}' has no labels file assigned."; return false; }
            problem = null;
            return true;
        }

        /// <summary>Label lines, with Windows line endings and trailing blank lines removed.</summary>
        public string[] GetLabelLines()
        {
            if (Labels == null) return System.Array.Empty<string>();
            var lines = Labels.text.Replace("\r", "").Split('\n');
            var count = lines.Length;
            while (count > 0 && string.IsNullOrWhiteSpace(lines[count - 1])) count--;
            System.Array.Resize(ref lines, count);
            return lines;
        }
    }
}
