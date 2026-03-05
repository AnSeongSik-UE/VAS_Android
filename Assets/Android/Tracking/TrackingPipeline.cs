// TrackingPipeline.cs
// Main controller for all tracking components
using UnityEngine;
using System.Collections.Generic;

public class TrackingPipeline : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.RawImage displayImage;
    [SerializeField] private TMPro.TMP_Text          debugText;
    [SerializeField] private BlazeFaceDetector faceDetector;
    [SerializeField] private BlazeHandDetector handDetector;
    [SerializeField] private HandLandmarkRunner handLandmarker;
    [SerializeField] private BlazePoseDetector poseDetector;
    [SerializeField] private BlazePoseLandmarkRunner poseLandmarker;
    [SerializeField] private ServerStreamSender serverSender;

    private WebCamTexture _cameraTexture;
    private TrackingPacket _currentPacket;

    // Tracking points storage for persistent rendering
    private Vector2[] _facePoints = new Vector2[6];
    private float _faceTime = -1f;
    private Vector2[] _handPoints = new Vector2[21];
    private float _handTime = -1f;
    private Vector2[] _posePoints = new Vector2[33];
    private float _poseTime = -1f;

    void Start()
    {
        try 
        {
            ValidateComponents();
            
            // Initialize packet and array
            _currentPacket = new TrackingPacket();
            _currentPacket.BlendShapeValues = new float[52];
            
            _ = RequestCameraAndStart();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TrackingPipeline] Start Error: " + e);
            if (debugText != null) debugText.text = "Start Error: " + e.Message;
        }
    }

    private async Awaitable RequestCameraAndStart()
    {
        try
        {
            // 런타임 카메라 권한 요청 (Android 6.0+ 필수)
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log("[TrackingPipeline] Requesting camera permission...");
                if (debugText != null) debugText.text = "Requesting camera permission...";
                await Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogError("[TrackingPipeline] Camera permission DENIED!");
                if (debugText != null) debugText.text = "ERROR: Camera permission denied!\nPlease grant camera access in Settings.";
                return;
            }

            Debug.Log("[TrackingPipeline] Camera permission granted.");

            string deviceName = "";
            foreach (var device in WebCamTexture.devices)
            {
                Debug.Log($"[TrackingPipeline] Found camera: {device.name} (front={device.isFrontFacing})");
                if (device.isFrontFacing) { deviceName = device.name; break; }
            }

            if (!string.IsNullOrEmpty(deviceName))
                _cameraTexture = new WebCamTexture(deviceName, 640, 480, 30);
            else
                _cameraTexture = new WebCamTexture(640, 480, 30);

            _cameraTexture.Play();
            Debug.Log($"[TrackingPipeline] Camera started: {_cameraTexture.deviceName} ({_cameraTexture.width}x{_cameraTexture.height})");

            _ = UpdateLoop();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[TrackingPipeline] Camera init error: " + e);
            if (debugText != null) debugText.text = "Camera Error: " + e.Message;
        }
    }

    private void SetupLayout()
    {
        if (displayImage == null || _cameraTexture == null) return;
        if (_cameraTexture.width <= 16) return;

        displayImage.texture = _cameraTexture;
        RectTransform rt = displayImage.rectTransform;

        var fitter = displayImage.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if (fitter != null) fitter.enabled = false;

        float cw       = _cameraTexture.width;
        float ch       = _cameraTexture.height;
        float rotation = _cameraTexture.videoRotationAngle;
        bool  isPortrait = (int)rotation % 180 != 0;

        rt.sizeDelta = new Vector2(cw, ch);

        float screenW = Screen.width;
        float screenH = Screen.height;
        float targetW = isPortrait ? screenH : screenW;
        float targetH = isPortrait ? screenW : screenH;
        float uniformScale = Mathf.Max(targetW / cw, targetH / ch);

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localEulerAngles = new Vector3(0, 0, -rotation);

        // 카메라
        float scaleX = uniformScale; 
        float scaleY = -uniformScale; 
        
        // Android/Unity 특정 플래폼에서 Y축 반전이 필요한 경우 대응
        //if (_cameraTexture.videoVerticallyMirrored) scaleY = uniformScale;
        
        rt.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private string _faceStatus = "<color=yellow><b>[FACE] OFF (YELLOW)</b></color>";
    private string _handStatus = "<color=green><b>[HAND] OFF (GREEN)</b></color>";
    private string _poseStatus = "<color=blue><b>[POSE] OFF (BLUE)</b></color>";

    private async Awaitable UpdateLoop()
    {
        var sb = new System.Text.StringBuilder();
        bool layoutDone = false;
        int trackingSlot = 0; // 0=Face, 1=Hand, 2=Pose (round-robin)
        
        while (_cameraTexture != null)
        {
            // Wait until camera is active
            if (!_cameraTexture.isPlaying) { await Awaitable.NextFrameAsync(); continue; }

            // Ensure Rich Text is enabled for coloring and set alignment
            if (debugText != null) 
            {
                debugText.richText = true;
                debugText.parseCtrlCharacters = true;
            }

            if (!layoutDone)
            {
                if (_cameraTexture.width > 16) { SetupLayout(); layoutDone = true; }
                else { await Awaitable.NextFrameAsync(); continue; }
            }

            try 
            {
                float startTime = Time.realtimeSinceStartup;
                sb.Clear();

                // 1. Mandatory component check
                if (faceDetector == null || handDetector == null || serverSender == null || 
                    handLandmarker == null || poseDetector == null || poseLandmarker == null)
                {
                    if (debugText != null) debugText.text = "Error: Missing Components!";
                    await Awaitable.NextFrameAsync();
                    continue;
                }

                // Ensure current packet internal array is valid
                if (_currentPacket.BlendShapeValues == null) _currentPacket.BlendShapeValues = new float[52];

                // Round-Robin: 프레임당 1개 모델만 실행하여 모바일 GPU 과부하 방지
                switch (trackingSlot)
                {
                    case 0: // Face Detection
                        var face = await faceDetector.DetectAsync(_cameraTexture);
                        _currentPacket.IsTracking = face.IsValid;
                        _faceStatus = face.IsValid ? "<color=yellow><b>[FACE] DETECTED (YELLOW)</b></color>" : "<color=yellow><b>[FACE] LOST (YELLOW)</b></color>";
                        if (face.IsValid && face.RawBoxes != null)
                        {
                            var bs = FaceKeyPointBlendShape.Calculate(face.RawBoxes); 
                            _currentPacket.BlendShapeValues[0] = bs.BlinkLeft;
                            _currentPacket.BlendShapeValues[1] = bs.BlinkRight;
                            UpdateFacePoints(face);
                        }
                        break;

                    case 1: // Hand Tracking
                        var hand = await handDetector.DetectAsync(_cameraTexture);
                        _handStatus = hand.IsValid ? "<color=green><b>[HAND] DETECTED (GREEN)</b></color>" : "<color=green><b>[HAND] LOST (GREEN)</b></color>";
                        if (hand.IsValid)
                        {
                            var handJoints = await handLandmarker.RunAsync(_cameraTexture, hand);
                            UpdateLandmarkPoints(handJoints, _handPoints, ref _handTime);
                        }
                        break;

                    case 2: // Pose Tracking
                        var pose = await poseDetector.DetectAsync(_cameraTexture);
                        _poseStatus = pose.IsValid ? "<color=blue><b>[POSE] DETECTED (BLUE)</b></color>" : "<color=blue><b>[POSE] LOST (BLUE)</b></color>";
                        if (pose.IsValid)
                        {
                            var poseJoints = await poseLandmarker.RunAsync(_cameraTexture, pose);
                            if (poseJoints != null && poseJoints.Length >= 15)
                            {
                                UpdatePoseData(poseJoints);
                                UpdateLandmarkPoints(poseJoints, _posePoints, ref _poseTime);
                            }
                        }
                        break;
                }

                trackingSlot = (trackingSlot + 1) % 3;

                // 통합 UI 업데이트 (3개 동시 표시)
                sb.AppendLine(_faceStatus);
                sb.AppendLine(_handStatus);
                sb.AppendLine(_poseStatus);

                // 통합 렌더링
                RenderAllPoints();

                // Network Send
                if (serverSender != null)
                {
                    _currentPacket.Timestamp = Time.time;
                    _ = serverSender.SendAsync(_currentPacket); 
                }

                float duration = (Time.realtimeSinceStartup - startTime) * 1000f;
                sb.AppendLine($"[TIME] {duration:F1} ms");
                if (debugText != null) debugText.text = sb.ToString();

                // Critical: Yield at the end of each iteration
                await Awaitable.NextFrameAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (debugText != null) debugText.text = "Loop Error: " + e.Message;
                await Awaitable.NextFrameAsync();
            }
        }
    }

    private List<UnityEngine.UI.Image> _pointPool = new();
    private int _activePointCount = 0;

    private void UpdateFacePoints(BlazeFaceDetector.FaceDetection face)
    {
        if (face.RawBoxes == null || face.RawBoxes.Length == 0) return;
        
        // 첫 번째 얼굴 기준
        // BlazeFace는 6개 키포인트를 가짐 (눈, 코, 입, 귀)
        for (int i=0; i<6; i++)
        {
            var kpTensor = face.AnchorPosition + new Unity.Mathematics.float2(face.RawBoxes[4 + i * 2], face.RawBoxes[4 + i * 2 + 1]);
            var kpTexture = BlazeUtils.mul(face.DetectorMatrix, kpTensor);
            
            _facePoints[i] = new Vector2(kpTexture.x / _cameraTexture.width, kpTexture.y / _cameraTexture.height);
        }
        _faceTime = Time.time;
    }

    private void UpdateLandmarkPoints(Vector3[] joints, Vector2[] outPoints, ref float outTime)
    {
        if (joints == null) return;
        int count = Mathf.Min(joints.Length, outPoints.Length);
        for (int i = 0; i < count; i++)
        {
            // Normalize texture space (pixels) -> normalized space (0~1)
            outPoints[i] = new Vector2(joints[i].x / _cameraTexture.width, joints[i].y / _cameraTexture.height);
        }
        outTime = Time.time;
    }

    private void RenderAllPoints()
    {
        ClearDebugPoints();
        float now = Time.time;
        float timeout = 0.5f;

        if (now - _faceTime < timeout)
        {
            for (int i = 0; i < 6; i++) SetPoint(_facePoints[i], Color.yellow);
        }
        if (now - _handTime < timeout)
        {
            for (int i = 0; i < 21; i++) SetPoint(_handPoints[i], Color.green);
        }
        if (now - _poseTime < timeout)
        {
            for (int i = 0; i < 33; i++) SetPoint(_posePoints[i], Color.blue);
        }
    }

    private void SetPoint(Vector2 normPos, Color color)
    {
        UnityEngine.UI.Image img;
        if (_activePointCount < _pointPool.Count)
        {
            img = _pointPool[_activePointCount];
        }
        else
        {
            var go = new GameObject("DebugPoint", typeof(UnityEngine.UI.Image));
            go.transform.SetParent(displayImage.transform, false);
            img = go.GetComponent<UnityEngine.UI.Image>();
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(10, 10);
            _pointPool.Add(img);
        }

        img.gameObject.SetActive(true);
        img.color = color;
        
        // 트래킹 포인트
        float x = (normPos.x - 0.5f) * displayImage.rectTransform.rect.width;
        float y = (normPos.y - 0.5f) * displayImage.rectTransform.rect.height;
        img.rectTransform.anchoredPosition = new Vector2(x, y);
        
        _activePointCount++;
    }

    private void ClearDebugPoints()
    {
        for (int i = 0; i < _pointPool.Count; i++)
        {
            _pointPool[i].gameObject.SetActive(false);
        }
        _activePointCount = 0;
    }

    private void UpdatePoseData(Vector3[] joints)
    {
        var nose           = joints[0];
        var leftEar        = joints[7];
        var rightEar       = joints[8];
        var leftShoulder   = joints[11];
        var rightShoulder  = joints[12];
        var lElbow         = joints[13];
        var rElbow         = joints[14];

        float yaw = Mathf.Atan2(rightEar.x - leftEar.x, rightEar.z - leftEar.z) * Mathf.Rad2Deg;
        var neckVec = nose - ((leftShoulder + rightShoulder) * 0.5f);
        float pitch = Mathf.Clamp(Mathf.Atan2(neckVec.y, Mathf.Abs(neckVec.z) + 0.001f) * Mathf.Rad2Deg, -45f, 45f);
        float roll = Mathf.Clamp(Mathf.Atan2(rightEar.y - leftEar.y, rightEar.x - leftEar.x) * Mathf.Rad2Deg, -30f, 30f);

        _currentPacket.HeadRotation = new Vector3(pitch, yaw, roll);

        var lArmVec = lElbow - leftShoulder;
        _currentPacket.LeftArmRotation = new Vector3(Mathf.Atan2(lArmVec.y, Mathf.Abs(lArmVec.z) + 0.001f) * Mathf.Rad2Deg, 0, (Mathf.Atan2(lArmVec.y, lArmVec.x) * Mathf.Rad2Deg) + 90f);

        var rArmVec = rElbow - rightShoulder;
        _currentPacket.RightArmRotation = new Vector3(Mathf.Atan2(rArmVec.y, Mathf.Abs(rArmVec.z) + 0.001f) * Mathf.Rad2Deg, 0, (Mathf.Atan2(rArmVec.y, rArmVec.x) * Mathf.Rad2Deg) - 90f);
    }

    private void ValidateComponents()
    {
        if (faceDetector == null)   faceDetector = GetComponent<BlazeFaceDetector>();
        if (handDetector == null)   handDetector = GetComponent<BlazeHandDetector>();
        if (handLandmarker == null) handLandmarker = GetComponent<HandLandmarkRunner>();
        if (poseDetector == null)   poseDetector = GetComponent<BlazePoseDetector>();
        if (poseLandmarker == null) poseLandmarker = GetComponent<BlazePoseLandmarkRunner>();
        if (serverSender == null)   serverSender = GetComponent<ServerStreamSender>();
    }
}

