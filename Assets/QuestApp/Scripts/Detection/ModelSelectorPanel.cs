using System.Collections.Generic;
using PassthroughCameraSamples.MultiObjectDetection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuestApp.Detection
{
    /// <summary>
    /// World-space menu that lists every DetectionModelProfile on the run manager as a button.
    /// Clicking one switches the live model. Works with any UI input (Interaction SDK ray/poke).
    /// </summary>
    public class ModelSelectorPanel : MonoBehaviour
    {
        [SerializeField] private SentisInferenceRunManager m_runManager;

        [Header("Layout")]
        [Tooltip("Parent with a VerticalLayoutGroup; a button is created here per model.")]
        [SerializeField] private Transform m_buttonContainer;
        [Tooltip("A disabled Button with a TMP_Text child, used as the template.")]
        [SerializeField] private Button m_buttonTemplate;

        [Header("Status")]
        [SerializeField] private TMP_Text m_activeModelLabel;
        [SerializeField] private TMP_Text m_activeModelDescription;
        [Tooltip("Shown while a model is loading (e.g. a spinner or 'Loading…' text).")]
        [SerializeField] private GameObject m_loadingIndicator;

        [Header("Colors")]
        [SerializeField] private Color m_activeColor = new(0.20f, 0.55f, 1f);
        [SerializeField] private Color m_inactiveColor = new(0.18f, 0.18f, 0.2f);

        [Header("Optional controller shortcut")]
        [Tooltip("Press to cycle models without pointing at the panel. Set to None to disable.")]
        [SerializeField] private OVRInput.Button m_cycleButton = OVRInput.Button.None;

        private readonly List<Button> m_buttons = new();

        private void OnEnable()
        {
            if (m_runManager == null) return;
            m_runManager.ModelSwitchStarted += OnSwitchStarted;
            m_runManager.ModelChanged += OnModelChanged;
        }

        private void OnDisable()
        {
            if (m_runManager == null) return;
            m_runManager.ModelSwitchStarted -= OnSwitchStarted;
            m_runManager.ModelChanged -= OnModelChanged;
        }

        private void Start()
        {
            BuildButtons();
            if (m_loadingIndicator) m_loadingIndicator.SetActive(false);
            if (m_runManager.ActiveProfile != null) OnModelChanged(m_runManager.ActiveProfile);
        }

        private void Update()
        {
            if (m_cycleButton != OVRInput.Button.None && OVRInput.GetDown(m_cycleButton) && !m_runManager.IsSwitchingModel)
            {
                m_runManager.SelectNextModel();
            }
        }

        private void BuildButtons()
        {
            m_buttonTemplate.gameObject.SetActive(false);
            var profiles = m_runManager.ModelProfiles;
            for (var i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null) continue;
                var index = i;
                var button = Instantiate(m_buttonTemplate, m_buttonContainer);
                button.name = $"Model_{profiles[i].DisplayName}";
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TMP_Text>().text = profiles[i].DisplayName;
                button.onClick.AddListener(() => m_runManager.SelectModel(index));
                m_buttons.Add(button);
            }
        }

        private void OnSwitchStarted(DetectionModelProfile next)
        {
            if (m_loadingIndicator) m_loadingIndicator.SetActive(true);
            if (m_activeModelLabel) m_activeModelLabel.text = $"Loading {next.DisplayName}…";
            foreach (var b in m_buttons) b.interactable = false;
        }

        private void OnModelChanged(DetectionModelProfile active)
        {
            if (m_loadingIndicator) m_loadingIndicator.SetActive(false);
            if (m_activeModelLabel) m_activeModelLabel.text = active.DisplayName;
            if (m_activeModelDescription) m_activeModelDescription.text = active.Description;

            var profiles = m_runManager.ModelProfiles;
            var buttonIndex = 0;
            for (var i = 0; i < profiles.Count && buttonIndex < m_buttons.Count; i++)
            {
                if (profiles[i] == null) continue;
                var button = m_buttons[buttonIndex++];
                button.interactable = true;
                if (button.targetGraphic) button.targetGraphic.color = profiles[i] == active ? m_activeColor : m_inactiveColor;
            }
        }
    }
}
