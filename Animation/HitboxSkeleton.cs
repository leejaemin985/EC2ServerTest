using LJMCollision;
using static HitboxDefinition;

/// <summary>
/// BoneAnimator + HitboxDefinition을 조합하여
/// 매 틱 캐릭터의 월드 공간 bone hitbox를 생성한다.
/// </summary>
public class HitboxSkeleton
{
    /// <summary>월드 공간에 배치된 개별 hitbox</summary>
    public struct WorldHitbox
    {
        public string Bone;
        public HitboxDefinition.HitboxShapeType Type;
        public Vec3 Center;
        public Quat Rotation;

        // shape별 파라미터
        public float Radius;     // Sphere, Capsule
        public float Height;     // Capsule
        public int Direction;    // Capsule (0=X, 1=Y, 2=Z)
        public Vec3 HalfSize;    // OBB
    }

    readonly BoneAnimator _animator;
    readonly HitboxDefinition _hitboxDefs;

    // bone 이름 → clip 내 bone index 캐시 (클립 변경 시 갱신)
    int[] _boneIndices;
    BakedAnimationData? _cachedClip;

    public HitboxSkeleton(BoneAnimator animator, HitboxDefinition hitboxDefs)
    {
        _animator = animator;
        _hitboxDefs = hitboxDefs;
        _boneIndices = new int[hitboxDefs.Hitboxes.Length];
    }

    /// <summary>
    /// 현재 애니메이션 프레임 기준으로 월드 공간 hitbox 목록을 계산한다.
    /// </summary>
    /// <param name="worldPos">캐릭터 월드 위치</param>
    /// <param name="worldRot">캐릭터 월드 회전</param>
    public List<WorldHitbox> Evaluate(Vec3 worldPos, Quat worldRot)
    {
        var clip = _animator.CurrentClip;
        if (clip == null) return new List<WorldHitbox>();

        // 클립이 바뀌면 bone index 캐시 갱신
        if (_cachedClip != clip)
        {
            _cachedClip = clip;
            for (int i = 0; i < _hitboxDefs.Hitboxes.Length; i++)
                _boneIndices[i] = clip.GetBoneIndex(_hitboxDefs.Hitboxes[i].Bone);
        }

        var results = new List<WorldHitbox>(_hitboxDefs.Hitboxes.Length);

        for (int i = 0; i < _hitboxDefs.Hitboxes.Length; i++)
        {
            int boneIdx = _boneIndices[i];
            if (boneIdx < 0) continue;

            var hb = _hitboxDefs.Hitboxes[i];

            // bake 데이터: root 기준 상대 좌표
            Vec3 localPos = _animator.GetBonePosition(boneIdx);
            Quat localRot = _animator.GetBoneRotation(boneIdx);

            // bone local offset 적용
            Vec3 localCenter = localPos + localRot.Rotate(hb.Offset);

            // 캐릭터 월드 transform 적용
            Vec3 worldCenter = worldPos + worldRot.Rotate(localCenter);
            Quat worldBoneRot = worldRot * localRot;

            var wh = new WorldHitbox
            {
                Bone = hb.Bone,
                Type = hb.Type,
                Center = worldCenter,
                Rotation = worldBoneRot,
            };

            switch (hb.Type)
            {
                case HitboxDefinition.HitboxShapeType.Sphere:
                    wh.Radius = hb.Radius;
                    break;
                case HitboxDefinition.HitboxShapeType.Capsule:
                    wh.Radius = hb.Radius;
                    wh.Height = hb.Height;
                    wh.Direction = hb.Direction;
                    break;
                case HitboxDefinition.HitboxShapeType.OBB:
                    wh.HalfSize = hb.Size * 0.5f;
                    break;
            }

            results.Add(wh);
        }

        return results;
    }
}
