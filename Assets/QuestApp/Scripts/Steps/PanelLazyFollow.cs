using UnityEngine;

namespace QuestApp.Steps
{
    /// <summary>
    /// Keeps a world-space panel comfortably in view: it stays put while you look around,
    /// and glides back in front of you once you turn or walk far enough away.
    /// Put it on the Canvas root of the step panel (and/or the model picker).
    /// </summary>
    public class PanelLazyFollow : MonoBehaviour
    {
        [Tooltip("Usually the CenterEyeAnchor. Falls back to Camera.main.")]
        [SerializeField] private Transform m_head;

        [SerializeField] private float m_distance = 0.6f;
        [Tooltip("Height offset from eye level (negative = below eyes, easier to read).")]
        [SerializeField] private float m_verticalOffset = -0.15f;
        [Tooltip("Horizontal offset (positive = to the right) so the panel doesn't block the work.")]
        [SerializeField] private float m_horizontalOffset = 0.25f;

        [Tooltip("Re-center once the panel is this many degrees off your gaze.")]
        [SerializeField] private float m_angleThreshold = 40f;
        [Tooltip("Re-center once the panel's resting spot is this far from where it should be.")]
        [SerializeField] private float m_distanceThreshold = 0.5f;
        [SerializeField] private float m_smoothTime = 0.3f;

        [Tooltip("Turn off to pin the panel where it is (e.g. via a 'Pin' button).")]
        public bool Following = true;

        private Vector3 m_velocity;
        private bool m_moving;

        private void Start()
        {
            if (m_head == null && Camera.main != null) m_head = Camera.main.transform;
            if (m_head != null) SnapToTarget();
        }

        private void LateUpdate()
        {
            if (!Following || m_head == null) return;

            var target = TargetPosition();
            if (!m_moving)
            {
                var toPanel = transform.position - m_head.position;
                var angle = Vector3.Angle(FlatForward(), Vector3.ProjectOnPlane(toPanel, Vector3.up));
                if (angle > m_angleThreshold || Vector3.Distance(transform.position, target) > m_distanceThreshold)
                {
                    m_moving = true;
                }
            }

            if (m_moving)
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref m_velocity, m_smoothTime);
                if (Vector3.Distance(transform.position, target) < 0.01f) m_moving = false;
            }

            FaceHead();
        }

        public void SetFollowing(bool follow) => Following = follow;
        public void ToggleFollowing() => Following = !Following;

        public void SnapToTarget()
        {
            transform.position = TargetPosition();
            FaceHead();
        }

        private Vector3 TargetPosition()
        {
            var forward = FlatForward();
            var right = Vector3.Cross(Vector3.up, forward);
            return m_head.position + forward * m_distance + right * m_horizontalOffset + Vector3.up * m_verticalOffset;
        }

        private Vector3 FlatForward()
        {
            var f = Vector3.ProjectOnPlane(m_head.forward, Vector3.up);
            return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
        }

        private void FaceHead()
        {
            var away = transform.position - m_head.position;
            away.y = 0;
            if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
