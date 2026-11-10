using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuestApp.Steps
{
    /// <summary>
    /// Drives a world-space step panel: title, body text, optional image, progress,
    /// and Back / Next / Restart buttons. Purely user-driven; nothing advances automatically.
    /// </summary>
    public class StepPanelController : MonoBehaviour
    {
        [SerializeField] private StepSequence m_sequence;

        [Header("Text")]
        [SerializeField] private TMP_Text m_sequenceTitleText;
        [SerializeField] private TMP_Text m_stepTitleText;
        [SerializeField] private TMP_Text m_stepBodyText;
        [Tooltip("e.g. 'Step 2 of 7'")]
        [SerializeField] private TMP_Text m_progressText;

        [Header("Visuals")]
        [SerializeField] private Image m_stepImage;
        [Tooltip("Optional Image with Image Type = Filled; fill shows progress.")]
        [SerializeField] private Image m_progressFill;

        [Header("Buttons")]
        [SerializeField] private Button m_backButton;
        [SerializeField] private Button m_nextButton;
        [SerializeField] private TMP_Text m_nextButtonLabel;
        [SerializeField] private Button m_restartButton;

        [Header("Wording")]
        [SerializeField] private string m_nextLabel = "Next";
        [SerializeField] private string m_finishLabel = "Finish";
        [SerializeField] private string m_completeTitle = "All steps complete";
        [SerializeField, TextArea] private string m_completeBody = "Nice work. Press Restart to go through the steps again.";

        [Header("Optional controller shortcuts (set to None to disable)")]
        [SerializeField] private OVRInput.Button m_nextShortcut = OVRInput.Button.None;
        [SerializeField] private OVRInput.Button m_backShortcut = OVRInput.Button.None;

        [Header("Events")]
        public UnityEvent<int> OnStepChanged;      // zero-based index
        public UnityEvent OnSequenceCompleted;

        private IReadOnlyList<Step> m_steps = new List<Step>();
        private string m_title;

        public int CurrentIndex { get; private set; }
        public bool IsComplete { get; private set; }
        public int StepCount => m_steps.Count;

        private void Awake()
        {
            if (m_backButton) m_backButton.onClick.AddListener(Previous);
            if (m_nextButton) m_nextButton.onClick.AddListener(Next);
            if (m_restartButton) m_restartButton.onClick.AddListener(Restart);
        }

        private void Start()
        {
            if (m_sequence != null) LoadSequence(m_sequence);
            else Refresh();
        }

        private void Update()
        {
            if (m_nextShortcut != OVRInput.Button.None && OVRInput.GetDown(m_nextShortcut)) Next();
            if (m_backShortcut != OVRInput.Button.None && OVRInput.GetDown(m_backShortcut)) Previous();
        }

        /// <summary>Swap in a different procedure at runtime (e.g. from a menu).</summary>
        public void LoadSequence(StepSequence sequence)
        {
            m_sequence = sequence;
            (m_title, m_steps) = sequence.Resolve();
            CurrentIndex = 0;
            IsComplete = false;
            Refresh();
            OnStepChanged?.Invoke(CurrentIndex);
        }

        public void Next()
        {
            if (IsComplete || m_steps.Count == 0) return;
            if (CurrentIndex < m_steps.Count - 1)
            {
                GoTo(CurrentIndex + 1);
            }
            else
            {
                IsComplete = true;
                Refresh();
                OnSequenceCompleted?.Invoke();
            }
        }

        public void Previous()
        {
            if (IsComplete) { IsComplete = false; Refresh(); OnStepChanged?.Invoke(CurrentIndex); return; }
            if (CurrentIndex > 0) GoTo(CurrentIndex - 1);
        }

        public void Restart() => GoTo(0);

        public void GoTo(int index)
        {
            if (m_steps.Count == 0) return;
            CurrentIndex = Mathf.Clamp(index, 0, m_steps.Count - 1);
            IsComplete = false;
            Refresh();
            OnStepChanged?.Invoke(CurrentIndex);
        }

        private void Refresh()
        {
            if (m_sequenceTitleText) m_sequenceTitleText.text = m_title ?? "";

            if (m_steps.Count == 0)
            {
                SetContent("No steps", "Assign a Step Sequence to this panel.", null);
                SetProgress("", 0f);
                SetButtons(back: false, next: false, restart: false);
                return;
            }

            if (IsComplete)
            {
                SetContent(m_completeTitle, m_completeBody, null);
                SetProgress($"{m_steps.Count} of {m_steps.Count}", 1f);
                SetButtons(back: true, next: false, restart: true);
                return;
            }

            var step = m_steps[CurrentIndex];
            SetContent(step.Title, step.Body, step.Image);
            SetProgress($"Step {CurrentIndex + 1} of {m_steps.Count}", (CurrentIndex + 1f) / m_steps.Count);

            var isLast = CurrentIndex == m_steps.Count - 1;
            SetButtons(back: CurrentIndex > 0, next: true, restart: CurrentIndex > 0);
            if (m_nextButtonLabel) m_nextButtonLabel.text = isLast ? m_finishLabel : m_nextLabel;
        }

        private void SetContent(string title, string body, Sprite image)
        {
            if (m_stepTitleText) m_stepTitleText.text = title;
            if (m_stepBodyText) m_stepBodyText.text = body;
            if (m_stepImage)
            {
                m_stepImage.sprite = image;
                m_stepImage.gameObject.SetActive(image != null);
                if (image != null) m_stepImage.preserveAspect = true;
            }
        }

        private void SetProgress(string label, float fraction)
        {
            if (m_progressText) m_progressText.text = label;
            if (m_progressFill) m_progressFill.fillAmount = fraction;
        }

        private void SetButtons(bool back, bool next, bool restart)
        {
            if (m_backButton) m_backButton.interactable = back;
            if (m_nextButton) m_nextButton.gameObject.SetActive(next);
            if (m_restartButton) m_restartButton.gameObject.SetActive(restart);
        }
    }
}
