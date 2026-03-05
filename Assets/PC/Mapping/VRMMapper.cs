// VRMMapper.cs (PC 앱)
// UE 비유: LiveLink에서 받은 트래킹 데이터를 MorphTarget/본에 적용하는 것과 동일
using UnityEngine;
using UniVRM10;

public class VRMMapper : MonoBehaviour
{
  // Inspector에서 VRM10 아바타의 최상단 컴포넌트를 드래그 앤 드롭으로 연결
  [SerializeField] private Vrm10Instance vrmInstance; // Vrm10Instance 컴포넌트
  [SerializeField] private Animator      vrmAnimator; // 본 접근용
  [SerializeField, Range(1f, 30f)] private float lerpSpeed = 15f; 

  private Quaternion _initialLeftArm;
  private Quaternion _initialRightArm;
  private Quaternion _initialNeck;
  private bool       _initialized = false;

  private TrackingPacket _latestPacket;
  private bool           _hasPacket = false;
  private float          _lastPacketTime = 0f;
  private float[]        _currentBlendShapes = new float[52];

  void Start()
  {
      InitializeBones();
  }

  private void InitializeBones()
  {
      if (vrmAnimator == null) return;
      var lArm = vrmAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
      var rArm = vrmAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
      var neck = vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);

      if (lArm != null) _initialLeftArm = lArm.localRotation;
      if (rArm != null) _initialRightArm = rArm.localRotation;
      if (neck != null) _initialNeck = neck.localRotation;
      _initialized = true;
      Debug.Log("[VRMMapper] Initial Bone Rotations Captured.");
  }

  public void Apply(TrackingPacket p)
  {
    if (p.BlendShapeValues == null) return;
    _latestPacket = p;
    _hasPacket = true;
    _lastPacketTime = Time.time;
  }

  void Update()
  {
      if (!_initialized) return;

      // 1. 타임아웃 체크 (3초간 패킷 미수신 시)
      bool isTimeout = _hasPacket && (Time.time - _lastPacketTime > 3f);
      
      if (isTimeout)
      {
          ReturnToInitialPose();
          LerpBlendShapes(new float[52]); 
          return;
      }

      if (_hasPacket)
      {
          int mask = _latestPacket.IsTracking; 

          // 2. 부위별 개별 처리
          // 얼굴/BlendShape
          if ((mask & 1) != 0) LerpBlendShapes(_latestPacket.BlendShapeValues);
          else LerpBlendShapes(new float[52]);

          // 머리 회전
          if ((mask & 1) != 0) SlerpHeadRotation(_latestPacket.HeadRotation);
          else SlerpHeadToDefault();

          // 팔 회전 및 손가락
          bool hasHand = (mask & 2) != 0;
          bool hasPose = (mask & 4) != 0;
          if (hasHand || hasPose) 
          {
              SlerpArmRotations(_latestPacket.LeftArmRotation, _latestPacket.RightArmRotation);
              if (hasHand) UpdateFingerCurls(_latestPacket.LeftFingerCurls, _latestPacket.RightFingerCurls);
          }
          else 
          {
              SlerpArmsToDefault();
          }
      }
  }

  private void SlerpHeadToDefault()
  {
      var neck = vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);
      if (neck != null) neck.localRotation = Quaternion.Slerp(neck.localRotation, _initialNeck, Time.deltaTime * lerpSpeed);
  }

  private void SlerpArmsToDefault()
  {
      var lArm = vrmAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
      var rArm = vrmAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
      float t = Time.deltaTime * lerpSpeed;
      if (lArm != null) lArm.localRotation = Quaternion.Slerp(lArm.localRotation, _initialLeftArm, t);
      if (rArm != null) rArm.localRotation = Quaternion.Slerp(rArm.localRotation, _initialRightArm, t);
  }

  private void ReturnToInitialPose()
  {
      float t = Time.deltaTime * lerpSpeed;
      var lArm = vrmAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
      var rArm = vrmAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
      var neck = vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);

      if (lArm != null) lArm.localRotation = Quaternion.Slerp(lArm.localRotation, _initialLeftArm, t);
      if (rArm != null) rArm.localRotation = Quaternion.Slerp(rArm.localRotation, _initialRightArm, t);
      if (neck != null) neck.localRotation = Quaternion.Slerp(neck.localRotation, _initialNeck, t);
  }

  private void SlerpArmRotations(Vector3 leftEuler, Vector3 rightEuler)
  {
    var lShoulder = vrmAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
    var rShoulder = vrmAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
    float t = Time.deltaTime * lerpSpeed;

    if (lShoulder != null) 
    {
        Quaternion targetL = _initialLeftArm * Quaternion.Euler(leftEuler.x, -leftEuler.y, -leftEuler.z);
        lShoulder.localRotation = Quaternion.Slerp(lShoulder.localRotation, targetL, t);
    }
    if (rShoulder != null) 
    {
        Quaternion targetR = _initialRightArm * Quaternion.Euler(rightEuler.x, -rightEuler.y, -rightEuler.z);
        rShoulder.localRotation = Quaternion.Slerp(rShoulder.localRotation, targetR, t);
    }
  }

  private void LerpBlendShapes(float[] targetVals)
  {
    float t = Time.deltaTime * lerpSpeed;
    for (int i=0; i<52; i++) {
        _currentBlendShapes[i] = Mathf.Lerp(_currentBlendShapes[i], targetVals[i], t);
    }
    
    // UI Expression 설정
    Set(ExpressionPreset.blinkLeft, Get(_currentBlendShapes, "eyeBlinkLeft"));
    Set(ExpressionPreset.blinkRight,Get(_currentBlendShapes, "eyeBlinkRight"));
    Set(ExpressionPreset.aa,        Mathf.Clamp01(Get(_currentBlendShapes, "jawOpen") * 1.2f));
    Set(ExpressionPreset.oh,        Mathf.Clamp01(Get(_currentBlendShapes, "mouthFunnel") + Get(_currentBlendShapes, "mouthPucker")));
    Set(ExpressionPreset.ou,        Mathf.Clamp01(Get(_currentBlendShapes, "mouthPucker") * 1.5f));
    Set(ExpressionPreset.happy,     (Get(_currentBlendShapes, "mouthSmileLeft") + Get(_currentBlendShapes, "mouthSmileRight")) * 0.5f);
    Set(ExpressionPreset.sad,       (Get(_currentBlendShapes, "mouthFrownLeft") + Get(_currentBlendShapes, "mouthFrownRight")) * 0.5f);
    Set(ExpressionPreset.surprised, Get(_currentBlendShapes, "browInnerUp"));
    Set(ExpressionPreset.angry,     (Get(_currentBlendShapes, "browDownLeft") + Get(_currentBlendShapes, "browDownRight")) * 0.5f);
  }

  private void SlerpHeadRotation(Vector3 euler)
  {
    var neck = vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);
    if (neck == null) return;

    Quaternion target = _initialNeck * Quaternion.Euler(-euler.x, -euler.y, euler.z);
    neck.localRotation = Quaternion.Slerp(neck.localRotation, target, Time.deltaTime * lerpSpeed);
  }

  private void Set(ExpressionPreset p, float v)
  {
    if (vrmInstance != null && vrmInstance.Runtime != null)
    {
      vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.CreateFromPreset(p), Mathf.Clamp01(v));
    }
  }

  private float Get(float[] v, string name) =>
    TrackingPacket.BlendShapeIndex.TryGetValue(name, out int i) ? v[i] : 0f;

  private void UpdateFingerCurls(float[] left, float[] right)
  {
      if (left != null && left.Length >= 5) SlerpFingerBones(true, left);
      if (right != null && right.Length >= 5) SlerpFingerBones(false, right);
  }

  private void SlerpFingerBones(bool isLeft, float[] curls)
  {
      float t = Time.deltaTime * lerpSpeed;
      HumanBodyBones[][] fingerBones = isLeft ? new HumanBodyBones[][] {
          new[] { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
          new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
          new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
          new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
          new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal }
      } : new HumanBodyBones[][] {
          new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
          new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
          new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
          new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
          new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal }
      };

      for (int i = 0; i < 5; i++)
      {
          float angle = curls[i] * 80f; // 최대 80도 굽힘
          foreach (var boneId in fingerBones[i])
          {
              var bone = vrmAnimator.GetBoneTransform(boneId);
              if (bone == null) continue;
              // VRM 축 기준: Z 또는 X축 굽힘 (여기서는 기본 -angle/angle 사용)
              bone.localRotation = Quaternion.Slerp(bone.localRotation, Quaternion.Euler(0, 0, isLeft ? angle : -angle), t);
          }
      }
  }
}
