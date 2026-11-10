using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestApp.Steps
{
    [Serializable]
    public class Step
    {
        public string Title;
        [TextArea(3, 10)] public string Body;
        [Tooltip("Optional picture or diagram for this step.")]
        public Sprite Image;
    }

    /// <summary>
    /// An ordered list of instructions the user clicks through.
    /// Author in the Inspector (Assets > Create > Quest App > Step Sequence),
    /// or import from JSON so non-Unity people can write steps.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStepSequence", menuName = "Quest App/Step Sequence")]
    public class StepSequence : ScriptableObject
    {
        public string SequenceTitle = "Procedure";
        public List<Step> Steps = new();

        [Header("Optional JSON source")]
        [Tooltip("If set, steps are read from this JSON at runtime instead of the list above. Format: see README.")]
        public TextAsset JsonSource;

        /// <summary>The title and steps to show, from JSON if assigned, otherwise from the Inspector list.</summary>
        public (string title, IReadOnlyList<Step> steps) Resolve()
        {
            if (JsonSource == null) return (SequenceTitle, Steps);
            var parsed = FromJson(JsonSource.text);
            return (string.IsNullOrEmpty(parsed.title) ? SequenceTitle : parsed.title, parsed.steps);
        }

        [Serializable]
        private class JsonStep { public string title; public string body; public string image; }

        [Serializable]
        private class JsonSequence { public string title; public List<JsonStep> steps = new(); }

        /// <summary>Images in JSON are paths under a Resources folder, without extension (e.g. "Steps/wrench").</summary>
        public static (string title, List<Step> steps) FromJson(string json)
        {
            var data = JsonUtility.FromJson<JsonSequence>(json) ?? new JsonSequence();
            var steps = new List<Step>();
            foreach (var s in data.steps)
            {
                steps.Add(new Step
                {
                    Title = s.title,
                    Body = s.body,
                    Image = string.IsNullOrEmpty(s.image) ? null : Resources.Load<Sprite>(s.image),
                });
            }
            return (data.title, steps);
        }
    }
}
