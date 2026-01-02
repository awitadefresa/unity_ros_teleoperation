using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine.InputSystem;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;
using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using TMPro;

public class HeadsetPublisher : MonoBehaviour
{
    public string unityFrame = "vr_origin";
    public string headsetFrame = "headset";
    public string handFrameLeft = "hand_left";
    public string poseTopic = "/quest/pose";
    public TextMeshProUGUI decimatorText;

    public InputActionReference headsetPose;
    public InputActionReference headsetRotation;
    public InputActionReference handPoseLeft;
    public InputActionReference handRotationLeft;
    public InputActionReference handPoseRight;
    public InputActionReference handRotationRight;

    private Transform root;
    private string handFrameRight = "hand_right";
    private ROSConnection ros;
    private TFMessageMsg tfMsg;
    private PoseStampedMsg headsetPoseMsg;
    private PoseStampedMsg leftHandMsg;
    private PoseStampedMsg rightHandMsg;

    private HeaderMsg headsetHeader;
    private HeaderMsg odomHeader;

    private string rootFrame = "odom";
    private int _decimator = 1;
    private int _frameCounter = 0;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        handFrameRight = handFrameLeft.Replace("left", "right");

        // Safe root lookup (no crash if tag is missing)
        GameObject rootGo = GameObject.FindWithTag("root");
        if (rootGo == null)
        {
            Debug.LogWarning("[HeadsetPublisher] Root with tag 'root' not found. Using rootFrame='odom' and identity transform.");
            root = null;
            rootFrame = "odom";
        }
        else
        {
            root = rootGo.transform;
            var tfAttach = root.GetComponent<TFAttachment>();
            if (tfAttach != null && !string.IsNullOrEmpty(tfAttach.FrameID))
                rootFrame = tfAttach.FrameID;
            else
                rootFrame = "odom";
        }

        // Enable actions if present (prevents some Android cases)
        EnableIfValid(headsetPose);
        EnableIfValid(headsetRotation);
        EnableIfValid(handPoseLeft);
        EnableIfValid(handRotationLeft);
        EnableIfValid(handPoseRight);
        EnableIfValid(handRotationRight);

        ros.RegisterPublisher<PoseStampedMsg>(poseTopic + "/headset");
        ros.RegisterPublisher<TFMessageMsg>("/tf");

        headsetPoseMsg = new PoseStampedMsg();
        leftHandMsg = new PoseStampedMsg();
        rightHandMsg = new PoseStampedMsg();

        headsetHeader = new HeaderMsg();
        headsetHeader.frame_id = unityFrame;

        odomHeader = new HeaderMsg();
        odomHeader.frame_id = rootFrame;

        tfMsg = new TFMessageMsg();

        if (decimatorText != null)
            decimatorText.text = "TF Decimator: " + _decimator;
    }

    private void EnableIfValid(InputActionReference a)
    {
        if (a != null && a.action != null)
            a.action.Enable();
    }

    void Update()
    {
        // Publish every nth frame
        _frameCounter++;
        if (_frameCounter % _decimator != 0)
            return;
        _frameCounter = 0;

        // If any required action is missing, do nothing (no crash)
        if (headsetPose == null || headsetPose.action == null ||
            headsetRotation == null || headsetRotation.action == null ||
            handPoseLeft == null || handPoseLeft.action == null ||
            handRotationLeft == null || handRotationLeft.action == null ||
            handPoseRight == null || handPoseRight.action == null ||
            handRotationRight == null || handRotationRight.action == null)
        {
            return;
        }

        // TF message (odom -> vr_origin, then headset/hands under vr_origin)
        tfMsg.transforms = new TransformStampedMsg[4];

        // 0) root -> unityFrame
        tfMsg.transforms[0] = new TransformStampedMsg();
        HeaderMsg rootHeader = new HeaderMsg();
        rootHeader.frame_id = rootFrame;
        tfMsg.transforms[0].header = rootHeader;
        tfMsg.transforms[0].child_frame_id = unityFrame;
        tfMsg.transforms[0].transform = new TransformMsg();
        tfMsg.transforms[0].transform.translation = new Vector3Msg();
        tfMsg.transforms[0].transform.rotation = new QuaternionMsg();

        if (root != null)
        {
            tfMsg.transforms[0].transform.translation = root.InverseTransformPoint(Vector3.zero).To<FLU>();
            tfMsg.transforms[0].transform.rotation = Quaternion.Inverse(root.rotation).To<FLU>();
        }
        else
        {
            tfMsg.transforms[0].transform.translation = Vector3.zero.To<FLU>();
            tfMsg.transforms[0].transform.rotation = Quaternion.identity.To<FLU>();
        }

        // 1) unityFrame -> headset
        tfMsg.transforms[1] = new TransformStampedMsg();
        tfMsg.transforms[1].header = headsetHeader;
        tfMsg.transforms[1].child_frame_id = headsetFrame;
        tfMsg.transforms[1].transform = new TransformMsg();
        tfMsg.transforms[1].transform.translation = new Vector3Msg();
        tfMsg.transforms[1].transform.rotation = new QuaternionMsg();
        tfMsg.transforms[1].transform.rotation.w = 1;

        // 2) unityFrame -> left hand
        tfMsg.transforms[2] = new TransformStampedMsg();
        tfMsg.transforms[2].header = headsetHeader;
        tfMsg.transforms[2].child_frame_id = handFrameLeft;
        tfMsg.transforms[2].transform = new TransformMsg();

        // 3) unityFrame -> right hand
        tfMsg.transforms[3] = new TransformStampedMsg();
        tfMsg.transforms[3].header = headsetHeader;
        tfMsg.transforms[3].child_frame_id = handFrameRight;
        tfMsg.transforms[3].transform = new TransformMsg();

        // Read headset pose/rotation from InputActions (this is what can stay at 0 on Quest)
        QuaternionMsg quaternion = headsetRotation.action.ReadValue<Quaternion>().To<FLU>();
        if (quaternion.From<FLU>().Equals(default))
            quaternion.w = 1;

        tfMsg.transforms[1].transform.translation = headsetPose.action.ReadValue<Vector3>().To<FLU>();
        tfMsg.transforms[1].transform.rotation = quaternion;

        tfMsg.transforms[2].transform.translation = handPoseLeft.action.ReadValue<Vector3>().To<FLU>();
        tfMsg.transforms[2].transform.rotation = handRotationLeft.action.ReadValue<Quaternion>().To<FLU>();

        tfMsg.transforms[3].transform.translation = handPoseRight.action.ReadValue<Vector3>().To<FLU>();
        tfMsg.transforms[3].transform.rotation = handRotationRight.action.ReadValue<Quaternion>().To<FLU>();

        // Fix default quaternions
        for (int i = 0; i < tfMsg.transforms.Length; i++)
        {
            if (tfMsg.transforms[i].transform.rotation.From<FLU>().Equals(default))
                tfMsg.transforms[i].transform.rotation.w = 1;
        }

        // PoseStamped headset
        headsetPoseMsg.pose.position = headsetPose.action.ReadValue<Vector3>().To<FLU>();
        headsetPoseMsg.pose.orientation = quaternion;

        // Publish (ROSConnection exists even if no ROS master; it won't NRE)
        ros.Publish(poseTopic + "/headset", headsetPoseMsg);
        ros.Publish("/tf", tfMsg);
    }

    public void OnDecimatorChange(float value)
    {
        _decimator = (int)value;
        if (decimatorText != null)
            decimatorText.text = "TF Decimator: " + _decimator;
    }
}
