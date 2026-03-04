// HandMapper.cs
// 손가락 관절 랜드마크를 애니메이터 본 회전값으로 변환
using UnityEngine;

public class HandMapper
{
    public static void Apply(Vector3[] joints, Animator animator, bool isLeft)
    {
        // 실전에서는 21개 관절 사이의 Vector3.Angle을 계산하여 각 손가락 마디 회전
        // 여기서는 개념적 구현만 포함
        var wrist = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (wrist != null) wrist.position = joints[0]; // 손목 위치 동기화
    }
}
