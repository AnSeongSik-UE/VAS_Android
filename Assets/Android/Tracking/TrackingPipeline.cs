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

        float scaleX = uniformScale;
        float scaleY = _cameraTexture.videoVerticallyMirrored ? -uniformScale : uniformScale;
        rt.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private async Awaitable UpdateLoop()
    {
        var sb = new System.Text.StringBuilder();
        bool layoutDone = false;
        int trackingSlot = 0; // 0=Face, 1=Hand, 2=Pose (round-robin)
        
        while (_cameraTexture != null)
        {
            // Wait until camera is active
            if (!_cameraTexture.isPlaying) { await Awaitable.NextFrameAsync(); continue; }

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
                    sb.AppendLine("Error: Missing Components!");
                    if (debugText != null) debugText.text = sb.ToString();
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
                        _currentPacket.IsTracking = face.IsValid; // 얼굴이 감지되어야 트래킹 유효
                        sb.AppendLine($"[FACE] {(face.IsValid ? "DETECTED" : "LOST")}");
                        if (face.IsValid && face.RawBoxes != null)
                        {
                            var bs = FaceKeyPointBlendShape.Calculate(face.RawBoxes); 
                            _currentPacket.BlendShapeValues[0] = bs.BlinkLeft;
                            _currentPacket.BlendShapeValues[1] = bs.BlinkRight;
                            
                            // 6개 주요 포인트 시각화
                            DrawFacePoints(face);
                        }
                        else { ClearDebugPoints(); }
                        break;

                    case 1: // Hand Tracking
                        var hand = await handDetector.DetectAsync(_cameraTexture);
                        if (hand.IsValid)
                        {
                            var handJoints = await handLandmarker.RunAsync(_cameraTexture, hand);
                            DrawLandmarkPoints(handJoints, Color.green);
                            sb.AppendLine("[HAND] DETECTED");
                        }
                        else
                        {
                            sb.AppendLine("[HAND] LOST");
                            ClearDebugPoints();
                        }
                        break;

                    case 2: // Pose Tracking
                        var pose = await poseDetector.DetectAsync(_cameraTexture);
                        if (pose.IsValid)
                        {
                            var poseJoints = await poseLandmarker.RunAsync(_cameraTexture, pose);
                            if (poseJoints != null && poseJoints.Length >= 15)
                            {
                                UpdatePoseData(poseJoints);
                                DrawLandmarkPoints(poseJoints, Color.cyan);
                                sb.AppendLine("[POSE] DETECTED");
                            }
                        }
                        else
                        {
                            sb.AppendLine("[POSE] LOST");
                            ClearDebugPoints();
                        }
                        break;
                }

                trackingSlot = (trackingSlot + 1) % 3;

                // Network Send (FIRE AND FORGET to prevent 1-frame freeze)
                if (serverSender != null)
                {
                    _currentPacket.Timestamp = Time.time;
                    _ = serverSender.SendAsync(_currentPacket); 
                    sb.AppendLine("[NET] OK");
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

    private void DrawFacePoints(BlazeFaceDetector.FaceDetection face)
    {
        ClearDebugPoints();
        if (face.RawBoxes == null || face.RawBoxes.Count == 0) return;
        
        var box = face.RawBoxes[0]; // 첫 번째 얼굴 기준
        // BlazeFace는 6개 키포인트를 가짐 (눈, 코, 입, 귀)
        for (int i=0; i<6; i++)
        {
            Vector2 kp = new Vector2(box.KeyPoints[i * 2], box.KeyPoints[i * 2 + 1]);
            SetPoint(kp, Color.yellow);
        }
    }

    private void DrawLandmarkPoints(Vector3[] joints, Color color)
    {
        ClearDebugPoints();
        if (joints == null) return;
        foreach (var joint in joints)
        {
            // Normalize image space (0~1) -> UI space
            SetPoint(new Vector2(joint.x, joint.y), color);
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
            img.rectTransform.sizeDelta = new Vector2(10, 10);
            _pointPool.Add(img);
        }

        img.gameObject.SetActive(true);
        img.color = color;
        
        // UI 좌표계 mapping (디스플레이 이미지 크기 기준)
        float x = (normPos.x - 0.5f) * displayImage.rectTransform.rect.width;
        float y = (0.5f - normPos.y) * displayImage.rectTransform.rect.height; // Y flip
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

