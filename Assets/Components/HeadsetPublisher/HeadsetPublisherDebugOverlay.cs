using System.Text;
using TMPro;
using UnityEngine;

public class HeadsetPublisherDebugOverlay : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text text;

    [Header("Rig / Frames")]
    public Transform xrOrigin;       // XR Origin (XR Rig)
    public Transform cameraTransform; // Main Camera (HMD)

    [Header("Hands (optional)")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Options")]
    public bool showLocalPoses = true;     // relative to xrOrigin
    public bool showWorldPoses = false;    // global
    public int decimals = 3;

    private float _dtAvg;
    private readonly StringBuilder _sb = new StringBuilder(1024);

    void Reset()
    {
        // Auto-fill common references if possible
        if (text == null) text = FindObjectOfType<TMP_Text>();
        if (xrOrigin == null)
        {
            var xrOriginObj = GameObject.Find("XR Origin (XR Rig)");
            if (xrOriginObj != null) xrOrigin = xrOriginObj.transform;
        }

        if (cameraTransform == null && xrOrigin != null)
        {
            var cam = xrOrigin.GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }
    }

    void Update()
    {
        if (text == null)
            return;

        // simple moving average fps
        _dtAvg = Mathf.Lerp(_dtAvg, Time.unscaledDeltaTime, 0.1f);
        var fps = (_dtAvg > 0f) ? 1f / _dtAvg : 0f;

        _sb.Clear();
        _sb.AppendLine("HeadsetPublisher Validation (Unity-only)");
        _sb.Append("FPS: ").Append(fps.ToString("F0")).AppendLine();
        _sb.AppendLine();

        AppendPose("XR_ORIGIN", xrOrigin, xrOrigin);
        AppendPose("HMD", cameraTransform, xrOrigin);
        AppendPose("LEFT", leftHand, xrOrigin);
        AppendPose("RIGHT", rightHand, xrOrigin);

        _sb.AppendLine();
        _sb.AppendLine("Expected axes note (from README):");
        _sb.AppendLine("HMD: x forward, y left, z up");
        _sb.AppendLine("Hands: x forward, y down, z right");

        text.text = _sb.ToString();
    }

    private void AppendPose(string label, Transform t, Transform origin)
    {
        if (t == null)
        {
            _sb.Append(label).Append(": (not set)\n");
            return;
        }

        _sb.Append(label).Append("  ");

        if (showLocalPoses && origin != null)
        {
            Vector3 p = origin.InverseTransformPoint(t.position);
            Quaternion q = Quaternion.Inverse(origin.rotation) * t.rotation;
            _sb.Append("Local P ").Append(Vec3(p)).Append("  R ").Append(QuatEuler(q)).Append("  ");
        }

        if (showWorldPoses)
        {
            _sb.Append("World P ").Append(Vec3(t.position)).Append("  R ").Append(Vec3(t.eulerAngles)).Append("  ");
        }

        _sb.AppendLine();
    }

    private string Vec3(Vector3 v)
    {
        return $"({v.x.ToString($"F{decimals}")}, {v.y.ToString($"F{decimals}")}, {v.z.ToString($"F{decimals}")})";
    }

    private string QuatEuler(Quaternion q)
    {
        var e = q.eulerAngles;
        return $"({e.x.ToString($"F{decimals}")}, {e.y.ToString($"F{decimals}")}, {e.z.ToString($"F{decimals}")})";
    }
}
