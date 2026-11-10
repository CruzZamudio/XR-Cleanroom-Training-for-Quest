// Based on Meta's PassthroughCameraApiSamples (c) Meta Platforms, Inc. and affiliates.
// Modified: saves next to the source ONNX under a chosen name (so custom models don't
// overwrite the stock COCO model) and works for any number of classes.

using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection.Editor
{
    [CustomEditor(typeof(SentisInferenceRunManager))]
    public class SentisModelEditorConverter : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            _ = DrawDefaultInspector();

            var runManager = (SentisInferenceRunManager)target;
            using (new EditorGUI.DisabledScope(runManager.OnnxModel == null))
            {
                if (GUILayout.Button("Convert ONNX → Sentis (adds box decoding for NMS)"))
                {
                    ConvertModel(runManager.OnnxModel, runManager.ConvertedModelName);
                }
            }
            if (runManager.OnnxModel == null)
            {
                EditorGUILayout.HelpBox("Assign an ONNX model above to enable conversion.", MessageType.Info);
            }
        }

        /// <summary>
        /// Expects Ultralytics-style output of shape (1, 4 + numClasses, N): YOLOv8, YOLOv9, YOLO11.
        /// Produces a model with three outputs: corner boxes (N,4), class IDs (N), scores (N).
        /// </summary>
        private static void ConvertModel(ModelAsset onnxAsset, string outputName)
        {
            var model = ModelLoader.Load(onnxAsset);

            var graph = new FunctionalGraph();
            var input = graph.AddInput(model, 0);

            var centersToCorners = Functional.Constant(new TensorShape(4, 4), new[]
            {
                1,      0,      1,      0,
                0,      1,      0,      1,
                -0.5f,  0,      0.5f,   0,
                0,      -0.5f,  0,      0.5f,
            });

            var modelOutput = Functional.Forward(model, input)[0];           // (1, 4+C, N)
            var boxCoords = modelOutput[0, ..4, ..].Transpose(0, 1);         // (N, 4) cx,cy,w,h
            var allScores = modelOutput[0, 4.., ..].Transpose(0, 1);         // (N, C)
            var scores = Functional.ReduceMax(allScores, 1);                 // (N)
            var classIDs = Functional.ArgMax(allScores, 1);                  // (N)
            var corners = Functional.MatMul(boxCoords, centersToCorners);    // (N, 4) x1,y1,x2,y2
            var modelFinal = graph.Compile(corners, classIDs, scores);

            ModelQuantizer.QuantizeWeights(QuantizationType.Uint8, ref modelFinal);

            var onnxPath = AssetDatabase.GetAssetPath(onnxAsset);
            var safeName = string.IsNullOrWhiteSpace(outputName) ? Path.GetFileNameWithoutExtension(onnxPath) + "_sentis" : outputName.Trim();
            var outPath = Path.Combine(Path.GetDirectoryName(onnxPath) ?? "Assets", safeName + ".sentis").Replace('\\', '/');

            if (File.Exists(outPath) && !EditorUtility.DisplayDialog("Overwrite model?", $"{outPath} already exists. Overwrite it?", "Overwrite", "Cancel"))
            {
                return;
            }

            ModelWriter.Save(outPath, modelFinal);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(outPath));
            Debug.Log($"[Detection] Converted model saved to {outPath}. Assign it to a Detection Model Profile.");
        }
    }
}
